using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Creates the <see cref="NavGrid"/> from <see cref="MapSettings"/> and re-stamps every <see cref="NavObstacle"/>
    /// whenever obstacles are added, removed or change archetype. Obstacles are assumed not to move.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(MovementSystemGroup), OrderFirst = true)]
    public partial struct NavGridSystem : ISystem
    {
        private EntityQuery _obstacles;
        private int _builtOrderVersion;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _obstacles = SystemAPI.QueryBuilder().WithAll<NavObstacle, LocalTransform>().Build();
            state.RequireForUpdate<MapSettings>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingletonRW<NavGrid>(out var grid) && grid.ValueRO.IsCreated)
            {
                grid.ValueRW.Cells.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var created = false;
            if (!SystemAPI.HasSingleton<NavGrid>())
            {
                CreateGrid(ref state, SystemAPI.GetSingleton<MapSettings>());
                created = true;
            }

            var orderVersion = state.EntityManager.GetComponentOrderVersion<NavObstacle>();
            if (!created && orderVersion == _builtOrderVersion)
            {
                return;
            }

            _builtOrderVersion = orderVersion;
            ref var grid = ref SystemAPI.GetSingletonRW<NavGrid>().ValueRW;
            grid.Version++;

            var clear = new ClearBytesJob { Cells = grid.Cells }.Schedule(state.Dependency);
            state.Dependency = new StampJob { Grid = grid }.Schedule(_obstacles, clear);
        }

        private static void CreateGrid(ref SystemState state, in MapSettings map)
        {
            var cellSize = math.max(0.1f, map.NavCellSize);
            var size = math.max(1, (int2)math.ceil(map.Size / cellSize));
            state.EntityManager.CreateSingleton(new NavGrid
            {
                Cells = new NativeArray<byte>(size.x * size.y, Allocator.Persistent),
                Size = size,
                Min = map.Min,
                CellSize = cellSize,
            }, "NavGrid");
        }

        // Single-threaded: overlapping footprints write the same cells.
        [BurstCompile]
        private partial struct StampJob : IJobEntity
        {
            public NavGrid Grid;

            private void Execute(in NavObstacle obstacle, in LocalTransform transform)
            {
                var half = new float3(obstacle.Size.x * 0.5f, 0f, obstacle.Size.y * 0.5f);
                var min = math.max(Grid.WorldToCell(transform.Position - half), 0);
                var max = math.min(Grid.WorldToCell(transform.Position + half - 0.001f), Grid.Size - 1);

                for (var y = min.y; y <= max.y; y++)
                {
                    for (var x = min.x; x <= max.x; x++)
                    {
                        Grid.Cells[Grid.Index(new int2(x, y))] = 1;
                    }
                }
            }
        }
    }
}
