using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Sectors;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation.Fields
{
    /// <summary>
    /// Rebuilds the <see cref="NavField"/> and <see cref="NavSectors"/> of sectors <see cref="NavGridSystem"/> marked
    /// dirty, and registers each agent's <see cref="MovementClass"/>. Only worlds that move units run it.
    /// </summary>
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(NavGridSystem))]
    [UpdateBefore(typeof(PathfindingSystem))]
    public partial struct NavFieldSystem : ISystem
    {
        private EntityQuery _agents;

        // Kept between frames and sized to the work by CollectJob, so an idle frame allocates nothing.
        private NativeList<int2> _rebuilds;
        private NativeList<int2> _relabels;
        private NativeList<FixedList4096Bytes<float>> _solved;
        private NativeList<FixedList512Bytes<byte>> _labelled;

        public void OnCreate(ref SystemState state)
        {
            _agents = SystemAPI.QueryBuilder().WithAll<NavAgent>().WithOptions(EntityQueryOptions.IncludePrefab)
                .Build();
            // Only new or edited agents can bring a new class.
            _agents.SetChangedVersionFilter(ComponentType.ReadOnly<NavAgent>());
            _rebuilds = new NativeList<int2>(Allocator.Persistent);
            _relabels = new NativeList<int2>(Allocator.Persistent);
            _solved = new NativeList<FixedList4096Bytes<float>>(Allocator.Persistent);
            _labelled = new NativeList<FixedList512Bytes<byte>>(Allocator.Persistent);
            state.RequireForUpdate<NavGrid>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
            _rebuilds.Dispose();
            _relabels.Dispose();
            _solved.Dispose();
            _labelled.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            ref var grid = ref SystemAPI.GetSingletonRW<NavGrid>().ValueRW;
            // The registry grows on the main thread, so only frames with new or edited agents wait for its readers;
            // the rest read it in jobs.
            var firstNewKey = int.MaxValue;
            if (!_agents.IsEmpty)
            {
                state.CompleteDependency();
                Register(ref grid);
                firstNewKey = grid.FitAccess();
            }

            state.Dependency = Rebuild(grid, firstNewKey, state.Dependency);
        }

        // Fields first, as relabelling reads them; then the sector graph of the sectors they marked.
        private JobHandle Rebuild(in NavGrid grid, int firstNewKey, JobHandle dependency)
        {
            dependency = new CollectJob
            {
                Field = grid.Field,
                SectorDirty = grid.Sectors.Dirty,
                Keys = grid.Classes.Keys,
                FirstNewKey = firstNewKey,
                ClassCount = grid.Sectors.ClassCount,
                Rebuilds = _rebuilds,
                Relabels = _relabels,
                Solved = _solved,
                Labelled = _labelled,
            }.Schedule(dependency);

            // One block per item, solved in parallel, then one job copies the blocks into the field.
            dependency = new RebuildJob
            {
                Cells = grid.Cells,
                Scenery = grid.Scenery,
                Keys = grid.Classes.Keys.AsDeferredJobArray(),
                Work = _rebuilds.AsDeferredJobArray(),
                Field = grid.Field,
                Solved = _solved.AsDeferredJobArray(),
            }.Schedule(_rebuilds, 1, dependency);
            dependency = new StoreFieldJob
            {
                Work = _rebuilds.AsDeferredJobArray(),
                Solved = _solved.AsDeferredJobArray(),
                Field = grid.Field,
            }.Schedule(dependency);

            return Relabel(grid, dependency);
        }

        private JobHandle Relabel(in NavGrid grid, JobHandle dependency)
        {
            dependency = new RelabelJob
            {
                Field = grid.Field,
                Classes = grid.Classes,
                Work = _relabels.AsDeferredJobArray(),
                Sectors = grid.Sectors,
                Labelled = _labelled.AsDeferredJobArray(),
            }.Schedule(_relabels, 1, dependency);
            dependency = new StoreSectorsJob
            {
                Work = _relabels.AsDeferredJobArray(),
                Labelled = _labelled.AsDeferredJobArray(),
                Sectors = grid.Sectors,
            }.Schedule(dependency);
            return new ConnectJob { Sectors = grid.Sectors }.Schedule(dependency);
        }

        private void Register(ref NavGrid grid)
        {
            var agents = _agents.ToComponentDataArray<NavAgent>(Allocator.Temp);
            foreach (var agent in agents)
            {
                // Aircraft never read the grid.
                if (agent.Layer != NavLayer.Air)
                {
                    grid.Classes.Register(agent.Class);
                }
            }

            agents.Dispose();
        }

        /// <summary>
        /// Every sector of a new key, else its dirty sectors, to rebuild; the marks pass on to the sector graph, which
        /// relabels every class of each of its dirty sectors, sector then class. Sizes the result blocks to match.
        /// </summary>
        [BurstCompile]
        private struct CollectJob : IJob
        {
            public NavField Field;
            public NativeArray<bool> SectorDirty;
            [ReadOnly] public NativeList<MovementClass> Keys;
            public int FirstNewKey;
            public int ClassCount;
            public NativeList<int2> Rebuilds;
            public NativeList<int2> Relabels;
            public NativeList<FixedList4096Bytes<float>> Solved;
            public NativeList<FixedList512Bytes<byte>> Labelled;

            public void Execute()
            {
                Rebuilds.Clear();
                Relabels.Clear();
                var dirty = Field.DirtySectors;
                for (var slot = 0; slot < Keys.Length; slot++)
                {
                    for (var sector = 0; sector < dirty.Length; sector++)
                    {
                        if (slot >= FirstNewKey || dirty[sector])
                        {
                            Rebuilds.Add(new int2(sector, slot));
                        }
                    }
                }

                for (var sector = 0; sector < dirty.Length; sector++)
                {
                    SectorDirty[sector] |= dirty[sector];
                    dirty[sector] = false;
                }

                for (var sector = 0; sector < SectorDirty.Length; sector++)
                {
                    for (var classIndex = 0; SectorDirty[sector] && classIndex < ClassCount; classIndex++)
                    {
                        Relabels.Add(new int2(sector, classIndex));
                    }
                }

                Solved.ResizeUninitialized(Rebuilds.Length);
                Labelled.ResizeUninitialized(Relabels.Length);
            }
        }

        /// <summary>One sector of one key per item, solved into that item's block.</summary>
        [BurstCompile]
        private struct RebuildJob : IJobParallelForDefer
        {
            private static readonly ProfilerMarker RebuildMarker = new("NavFieldSystem.Rebuild");

            [ReadOnly] public NativeArray<byte> Cells;
            [ReadOnly] public NativeArray<byte> Scenery;
            [ReadOnly] public NativeArray<MovementClass> Keys;

            /// <summary>Sector, then field slot.</summary>
            [ReadOnly] public NativeArray<int2> Work;

            [ReadOnly] public NavField Field;

            public NativeArray<FixedList4096Bytes<float>> Solved;

            public void Execute(int index)
            {
                using var scope = RebuildMarker.Auto();
                var item = Work[index];
                var transform = new DistanceTransform(Field.WindowSide, Allocator.Temp);
                var values = new FixedList4096Bytes<float>();
                Field.Solve(Cells, Scenery, Keys[item.y], item.x, ref transform, ref values);
                Solved[index] = values;
                transform.Dispose();
            }
        }

        [BurstCompile]
        private struct StoreFieldJob : IJob
        {
            [ReadOnly] public NativeArray<int2> Work;
            [ReadOnly] public NativeArray<FixedList4096Bytes<float>> Solved;
            public NavField Field;

            public void Execute()
            {
                for (var index = 0; index < Work.Length; index++)
                {
                    Field.Store(Work[index].y, Work[index].x, Solved[index]);
                }
            }
        }

        /// <summary>One sector of one class per item, labelled into that item's block.</summary>
        [BurstCompile]
        private struct RelabelJob : IJobParallelForDefer
        {
            private static readonly ProfilerMarker RelabelMarker = new("NavSectors.Relabel");

            [ReadOnly] public NavField Field;
            [ReadOnly] public MovementClasses Classes;

            /// <summary>Sector, then class.</summary>
            [ReadOnly] public NativeArray<int2> Work;

            [ReadOnly] public NavSectors Sectors;

            public NativeArray<FixedList512Bytes<byte>> Labelled;

            public void Execute(int index)
            {
                using var scope = RelabelMarker.Auto();
                var item = Work[index];
                var labelled = new FixedList512Bytes<byte>();
                Sectors.Labelling.Label(Field, Classes, item.y, item.x, ref labelled);
                Labelled[index] = labelled;
            }
        }

        [BurstCompile]
        private struct StoreSectorsJob : IJob
        {
            [ReadOnly] public NativeArray<int2> Work;
            [ReadOnly] public NativeArray<FixedList512Bytes<byte>> Labelled;
            public NavSectors Sectors;

            public void Execute()
            {
                for (var index = 0; index < Work.Length; index++)
                {
                    Sectors.Labelling.Store(Work[index].y, Work[index].x, Labelled[index]);
                }
            }
        }

        [BurstCompile]
        private struct ConnectJob : IJob
        {
            public NavSectors Sectors;

            public void Execute() => Sectors.Connect();
        }
    }
}
