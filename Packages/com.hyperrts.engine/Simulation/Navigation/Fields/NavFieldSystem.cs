using HyperRTS.Simulation.Common;
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

        public void OnCreate(ref SystemState state)
        {
            _agents = SystemAPI.QueryBuilder().WithAll<NavAgent>().WithOptions(EntityQueryOptions.IncludePrefab)
                .Build();
            // Only new or edited agents can bring a new class.
            _agents.SetChangedVersionFilter(ComponentType.ReadOnly<NavAgent>());
            state.RequireForUpdate<NavGrid>();
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
        private static JobHandle Rebuild(in NavGrid grid, int firstNewKey, JobHandle dependency)
        {
            var rebuilds = new NativeList<int2>(Allocator.TempJob);
            var relabels = new NativeList<int2>(Allocator.TempJob);
            dependency = new CollectJob
            {
                Field = grid.Field,
                SectorDirty = grid.Sectors.Dirty,
                Keys = grid.Classes.Keys,
                FirstNewKey = firstNewKey,
                ClassCount = grid.Sectors.ClassCount,
                Rebuilds = rebuilds,
                Relabels = relabels,
            }.Schedule(dependency);
            dependency = RebuildFields(grid, rebuilds, dependency);
            dependency = RelabelSectors(grid, relabels, dependency);
            dependency = new ConnectJob { Sectors = grid.Sectors }.Schedule(dependency);
            dependency = relabels.Dispose(dependency);
            return rebuilds.Dispose(dependency);
        }

        // Each item solves into its own range of a scratch array, then one job copies the rows into the field. Items
        // are only known on the worker, so the scratch holds the most there could be and unused ranges stay untouched.
        private static JobHandle RebuildFields(in NavGrid grid, NativeList<int2> rebuilds, JobHandle dependency)
        {
            var most = grid.Field.DirtySectors.Length * grid.Field.SlotCount;
            var solved = new NativeArray<float>(most * NavField.SectorArea, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            dependency = new RebuildJob
            {
                Cells = grid.Cells,
                Scenery = grid.Scenery,
                Keys = grid.Classes.Keys.AsDeferredJobArray(),
                Work = rebuilds.AsDeferredJobArray(),
                Field = grid.Field,
                Solved = solved,
            }.ScheduleBatch(solved.Length, NavField.SectorArea, dependency);
            dependency = new StoreFieldJob
            {
                Work = rebuilds.AsDeferredJobArray(),
                Solved = solved,
                Field = grid.Field,
            }.Schedule(dependency);
            return solved.Dispose(dependency);
        }

        // Same as the field: one range per item, then one job copies them into the sector graph.
        private static JobHandle RelabelSectors(in NavGrid grid, NativeList<int2> relabels, JobHandle dependency)
        {
            var most = grid.Sectors.SectorCount * grid.Sectors.ClassCount;
            var labelled = new NativeArray<byte>(most * NavSectors.LabelledBytes, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            dependency = new RelabelJob
            {
                Field = grid.Field,
                Classes = grid.Classes,
                Work = relabels.AsDeferredJobArray(),
                Sectors = grid.Sectors,
                Labelled = labelled,
            }.ScheduleBatch(labelled.Length, NavSectors.LabelledBytes, dependency);
            dependency = new StoreSectorsJob
            {
                Work = relabels.AsDeferredJobArray(),
                Labelled = labelled,
                Sectors = grid.Sectors,
            }.Schedule(dependency);
            return labelled.Dispose(dependency);
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
        /// relabels every class of each of its dirty sectors, sector then class.
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

            public void Execute()
            {
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
            }
        }

        /// <summary>One sector of one key per batch, solved into that batch's range of the scratch.</summary>
        [BurstCompile]
        private struct RebuildJob : IJobParallelForBatch
        {
            private static readonly ProfilerMarker RebuildMarker = new("NavFieldSystem.Rebuild");

            [ReadOnly] public NativeArray<byte> Cells;
            [ReadOnly] public NativeArray<byte> Scenery;
            [ReadOnly] public NativeArray<MovementClass> Keys;

            /// <summary>Sector, then field slot.</summary>
            [ReadOnly] public NativeArray<int2> Work;

            [ReadOnly] public NavField Field;

            public NativeArray<float> Solved;

            public void Execute(int startIndex, int count)
            {
                var index = startIndex / NavField.SectorArea;
                if (index >= Work.Length)
                {
                    return;
                }

                using var scope = RebuildMarker.Auto();
                var item = Work[index];
                var transform = new DistanceTransform(Field.WindowSide, Allocator.Temp);
                Field.Solve(Cells, Scenery, Keys[item.y], item.x, ref transform, Solved, startIndex);
                transform.Dispose();
            }
        }

        [BurstCompile]
        private struct StoreFieldJob : IJob
        {
            [ReadOnly] public NativeArray<int2> Work;
            [ReadOnly] public NativeArray<float> Solved;
            public NavField Field;

            public void Execute()
            {
                for (var index = 0; index < Work.Length; index++)
                {
                    Field.Store(Work[index].y, Work[index].x, Solved, index * NavField.SectorArea);
                }
            }
        }

        /// <summary>One sector of one class per batch, labelled into that batch's range of the scratch.</summary>
        [BurstCompile]
        private struct RelabelJob : IJobParallelForBatch
        {
            private static readonly ProfilerMarker RelabelMarker = new("NavSectors.Relabel");

            [ReadOnly] public NavField Field;
            [ReadOnly] public MovementClasses Classes;

            /// <summary>Sector, then class.</summary>
            [ReadOnly] public NativeArray<int2> Work;

            [ReadOnly] public NavSectors Sectors;

            public NativeArray<byte> Labelled;

            public void Execute(int startIndex, int count)
            {
                var index = startIndex / NavSectors.LabelledBytes;
                if (index >= Work.Length)
                {
                    return;
                }

                using var scope = RelabelMarker.Auto();
                var item = Work[index];
                Sectors.Label(Field, Classes, item.y, item.x, Labelled, startIndex);
            }
        }

        [BurstCompile]
        private struct StoreSectorsJob : IJob
        {
            [ReadOnly] public NativeArray<int2> Work;
            [ReadOnly] public NativeArray<byte> Labelled;
            public NavSectors Sectors;

            public void Execute()
            {
                for (var index = 0; index < Work.Length; index++)
                {
                    Sectors.Store(Work[index].y, Work[index].x, Labelled, index * NavSectors.LabelledBytes);
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
