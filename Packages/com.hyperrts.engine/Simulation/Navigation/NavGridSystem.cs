using HyperRTS.Core;
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
    /// Creates the <see cref="NavGrid"/> from <see cref="MapSettings"/> (terrain, water level and slope give each cell
    /// its base surface), then re-stamps every <see cref="NavArea"/> and <see cref="NavObstacle"/> over it whenever
    /// any are added, removed or change archetype. Areas and obstacles are assumed not to move.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(MovementSystemGroup), OrderFirst = true)]
    public partial struct NavGridSystem : ISystem
    {
        private EntityQuery _obstacles;
        private EntityQuery _areas;
        private NativeArray<byte> _base;
        private int _builtObstacleVersion;
        private int _builtAreaVersion;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _obstacles = SystemAPI.QueryBuilder().WithAll<NavObstacle, LocalTransform>().Build();
            _areas = SystemAPI.QueryBuilder().WithAll<NavArea, LocalTransform>().Build();
            state.RequireForUpdate<MapSettings>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingletonRW<NavGrid>(out var grid) && grid.ValueRO.IsCreated)
            {
                grid.ValueRW.Dispose();
            }

            if (_base.IsCreated)
            {
                _base.Dispose();
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

            var changed = TrackChanges(ref state);
            if (!created && !changed)
            {
                return;
            }

            ref var grid = ref SystemAPI.GetSingletonRW<NavGrid>().ValueRW;
            grid.Version++;

            state.Dependency = new ResetJob { Base = _base, Cells = grid.Cells }.Schedule(state.Dependency);
            state.Dependency = new AreaJob { Grid = grid, Decks = false }.Schedule(_areas, state.Dependency);
            state.Dependency = new AreaJob { Grid = grid, Decks = true }.Schedule(_areas, state.Dependency);
            state.Dependency = new ObstacleJob { Grid = grid }.Schedule(_obstacles, state.Dependency);
        }

        /// <summary>True when areas or obstacles were added, removed or changed archetype since the last stamp.</summary>
        private bool TrackChanges(ref SystemState state)
        {
            var obstacles = state.EntityManager.GetComponentOrderVersion<NavObstacle>();
            var areas = state.EntityManager.GetComponentOrderVersion<NavArea>();
            var changed = obstacles != _builtObstacleVersion || areas != _builtAreaVersion;
            _builtObstacleVersion = obstacles;
            _builtAreaVersion = areas;
            return changed;
        }

        private void CreateGrid(ref SystemState state, in MapSettings map)
        {
            var cellSize = math.max(0.1f, map.NavCellSize);
            var size = math.max(1, (int2)math.ceil(map.Size / cellSize));
            var count = size.x * size.y;
            SystemAPI.TryGetSingleton<TerrainHeight>(out var terrain);
            var grid = new NavGrid
            {
                Cells = new NativeArray<byte>(count, Allocator.Persistent),
                DeckHeights = new NativeArray<float>(count, Allocator.Persistent),
                Terrain = terrain,
                WaterLevel = map.WaterLevel,
                Size = size,
                Min = map.Min,
                CellSize = cellSize,
            };
            state.EntityManager.CreateSingleton(grid, "NavGrid");

            if (_base.IsCreated)
            {
                _base.Dispose();
            }

            _base = new NativeArray<byte>(count, Allocator.Persistent);
            var steep = map.MaxSlope > 0f && map.MaxSlope < 90f;
            state.Dependency = new ClassifyJob
            {
                Grid = grid,
                Base = _base,
                MaxGradient = steep ? math.tan(math.radians(map.MaxSlope)) : float.PositiveInfinity,
            }.Schedule(count, 1024, state.Dependency);
        }

        /// <summary>Terrain under the water level is water; land steeper than the slope limit is blocked.</summary>
        [BurstCompile]
        private struct ClassifyJob : IJobParallelFor
        {
            [ReadOnly] public NavGrid Grid;
            public NativeArray<byte> Base;
            public float MaxGradient;

            public void Execute(int index)
            {
                var center = Grid.CellCenter(Grid.Cell(index)).xz;
                if (Grid.Terrain.Height(center) < Grid.WaterLevel)
                {
                    Base[index] = (byte)NavSurface.Water;
                    return;
                }

                Base[index] = (byte)(IsTooSteep(center) ? NavSurface.Blocked : NavSurface.Land);
            }

            private readonly bool IsTooSteep(float2 center)
            {
                var half = Grid.CellSize * 0.5f;
                var terrain = Grid.Terrain;
                var dx = terrain.Height(center + new float2(half, 0f)) - terrain.Height(center - new float2(half, 0f));
                var dz = terrain.Height(center + new float2(0f, half)) - terrain.Height(center - new float2(0f, half));
                return math.length(new float2(dx, dz)) > MaxGradient * Grid.CellSize;
            }
        }

        [BurstCompile]
        private struct ResetJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Base;
            public NativeArray<byte> Cells;

            public void Execute() => Cells.CopyFrom(Base);
        }

        /// <summary>Single-threaded: overlapping areas write the same cells. Decks go last so they span water areas.</summary>
        [BurstCompile]
        private partial struct AreaJob : IJobEntity
        {
            public NavGrid Grid;
            public bool Decks;

            private void Execute(in NavArea area, in LocalTransform transform)
            {
                if ((area.Kind == NavAreaKind.Deck) != Decks)
                {
                    return;
                }

                Grid.GetArea(transform.Position, area.Size, out var min, out var max);
                min = math.max(min, 0);
                max = math.min(max, Grid.Size - 1);
                for (var y = min.y; y <= max.y; y++)
                {
                    for (var x = min.x; x <= max.x; x++)
                    {
                        var index = Grid.Index(new int2(x, y));
                        Grid.Cells[index] = Stamp(area.Kind, (NavSurface)Grid.Cells[index]);
                        if (Decks)
                        {
                            Grid.DeckHeights[index] = transform.Position.y;
                        }
                    }
                }
            }

            private static byte Stamp(NavAreaKind kind, NavSurface cell)
            {
                switch (kind)
                {
                    case NavAreaKind.Water:
                        return (byte)NavSurface.Water;
                    case NavAreaKind.Blocked:
                        return (byte)NavSurface.Blocked;
                    default:
                        return (byte)((cell & NavSurface.Water) | NavSurface.Land | NavSurface.Deck);
                }
            }
        }

        /// <summary>Single-threaded: overlapping footprints write the same cells.</summary>
        [BurstCompile]
        private partial struct ObstacleJob : IJobEntity
        {
            public NavGrid Grid;

            private void Execute(in NavObstacle obstacle, in LocalTransform transform)
            {
                Grid.GetArea(transform.Position, obstacle.Size, out var min, out var max);
                min = math.max(min, 0);
                max = math.min(max, Grid.Size - 1);
                for (var y = min.y; y <= max.y; y++)
                {
                    for (var x = min.x; x <= max.x; x++)
                    {
                        Grid.Cells[Grid.Index(new int2(x, y))] = (byte)NavSurface.Blocked;
                    }
                }
            }
        }
    }
}
