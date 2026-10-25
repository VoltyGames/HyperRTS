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
    /// and relabels the regions. A new key builds whole. Only worlds that move units run it.
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
            // The registry and dirty marks are read here on the main thread, after the grid's stamps.
            state.CompleteDependency();
            var classes = grid.Classes.Classes.Length;
            Register(ref grid);
            var firstNewKey = grid.FitAccess();
            var work = Collect(grid, firstNewKey);
            if (work.Length == 0 && classes == grid.Classes.Classes.Length)
            {
                work.Dispose();
                return;
            }

            var dependency = new RebuildJob
            {
                Cells = grid.Cells,
                Scenery = grid.Scenery,
                Keys = grid.Classes.Keys.AsArray(),
                Work = work.AsArray(),
                Field = grid.Field,
            }.Schedule(work.Length, 1, state.Dependency);
            dependency = grid.Regions.Schedule(grid.Field, grid.Classes, dependency);
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

        // Every sector of a new key, else its dirty sectors; the marks are spent once collected.
        private static NativeList<int2> Collect(in NavGrid grid, int firstNewKey)
        {
            var field = grid.Field;
            var work = new NativeList<int2>(field.DirtySectors.Length, Allocator.TempJob);
            for (var slot = 0; slot < grid.Classes.Keys.Length; slot++)
            {
                for (var sector = 0; sector < field.DirtySectors.Length; sector++)
                {
                    if (slot >= firstNewKey || field.DirtySectors[sector])
                    {
                        work.Add(new int2(sector, slot));
                    }
                }
            }

            for (var sector = 0; sector < field.DirtySectors.Length; sector++)
            {
                field.DirtySectors[sector] = false;
            }

            return work;
        }

        /// <summary>One sector of one key per index; each writes only that sector's cells of that key.</summary>
        [BurstCompile]
        private struct RebuildJob : IJobParallelFor
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
