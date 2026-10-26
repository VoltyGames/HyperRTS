using HyperRTS.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Registers the <see cref="MovementClass"/> of every <see cref="NavAgent"/> prefab and unit, then rebuilds the
    /// <see cref="NavField"/> for every sector <see cref="NavGridSystem"/> marked dirty, one job per sector and key,
    /// then relabels those sectors' <see cref="NavSectors"/>. A new key builds whole. Only worlds that move units run it.
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

            var work = new NativeList<int2>(Allocator.TempJob);
            var dependency = new CollectJob
            {
                Field = grid.Field,
                SectorDirty = grid.Sectors.Dirty,
                Keys = grid.Classes.Keys,
                FirstNewKey = firstNewKey,
                Work = work,
            }.Schedule(state.Dependency);
            dependency = new RebuildJob
            {
                Cells = grid.Cells,
                Scenery = grid.Scenery,
                Keys = grid.Classes.Keys.AsDeferredJobArray(),
                Work = work.AsDeferredJobArray(),
                Field = grid.Field,
            }.Schedule(work, 1, dependency);
            dependency = grid.Sectors.Schedule(grid.Field, grid.Classes, dependency);
            state.Dependency = work.Dispose(dependency);
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

        /// <summary>Every sector of a new key, else its dirty sectors; the marks pass on to the sector graph.</summary>
        [BurstCompile]
        private struct CollectJob : IJob
        {
            public NavField Field;
            public NativeArray<bool> SectorDirty;
            [ReadOnly] public NativeList<MovementClass> Keys;
            public int FirstNewKey;
            public NativeList<int2> Work;

            public void Execute()
            {
                var dirty = Field.DirtySectors;
                for (var slot = 0; slot < Keys.Length; slot++)
                {
                    for (var sector = 0; sector < dirty.Length; sector++)
                    {
                        if (slot >= FirstNewKey || dirty[sector])
                        {
                            Work.Add(new int2(sector, slot));
                        }
                    }
                }

                for (var sector = 0; sector < dirty.Length; sector++)
                {
                    SectorDirty[sector] |= dirty[sector];
                    dirty[sector] = false;
                }
            }
        }

        /// <summary>One sector of one key per index; each writes only that sector's cells of that key.</summary>
        [BurstCompile]
        private struct RebuildJob : IJobParallelForDefer
        {
            private static readonly ProfilerMarker RebuildMarker = new("NavFieldSystem.Rebuild");

            [ReadOnly] public NativeArray<byte> Cells;
            [ReadOnly] public NativeArray<byte> Scenery;
            [ReadOnly] public NativeArray<MovementClass> Keys;

            /// <summary>Sector, then field slot.</summary>
            [ReadOnly] public NativeArray<int2> Work;

            [NativeDisableParallelForRestriction] public NavField Field;

            public void Execute(int index)
            {
                using var scope = RebuildMarker.Auto();
                var item = Work[index];
                var transform = new DistanceTransform(Field.WindowSide, Allocator.Temp);
                Field.Rebuild(Cells, Scenery, Keys[item.y], item.y, item.x, ref transform);
                transform.Dispose();
            }
        }
    }
}
