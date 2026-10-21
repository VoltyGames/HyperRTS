using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Builds the <see cref="NavGrid"/> from <see cref="MapSettings"/> (terrain, slope and, when flooding, water
    /// level give each cell its base surface), rebuilding it when the map or <see cref="TerrainHeight"/> changes. Then
    /// re-stamps every <see cref="NavArea"/> and <see cref="NavObstacle"/> over it whenever any are added, removed or
    /// change archetype, and rebuilds the clearance field. Destructible scenery is stamped on its own layer, which
    /// changes without bumping the version. None of them are assumed to move.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(MovementSystemGroup), OrderFirst = true)]
    public partial struct NavGridSystem : ISystem
    {
        private EntityQuery _obstacles;
        private EntityQuery _areas;
        private EntityQuery _scenery;
        private NativeArray<byte> _base;
        private MapSettings _builtMap;
        private TerrainHeight _builtTerrain;
        private int _builtObstacleVersion;
        private int _builtAreaVersion;
        private int _builtSceneryVersion;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _obstacles = SystemAPI.QueryBuilder().WithAll<NavObstacle, LocalTransform>().Build();
            _areas = SystemAPI.QueryBuilder().WithAll<NavArea, LocalTransform>().Build();
            _scenery = SystemAPI.QueryBuilder().WithAll<Destructible, LocalTransform>()
                .WithNone<NavObstacle, NavAgent>().Build();
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
            var map = SystemAPI.GetSingleton<MapSettings>();
            SystemAPI.TryGetSingleton<TerrainHeight>(out var terrain);
            var rebuilt = !SystemAPI.HasSingleton<NavGrid>() || !SameGround(map, terrain);
            if (rebuilt)
            {
                _builtMap = map;
                _builtTerrain = terrain;
                BuildGrid(ref state, map, terrain);
            }

            var obstaclesChanged = OrderChanged<NavObstacle>(ref state, ref _builtObstacleVersion);
            var restamp = OrderChanged<NavArea>(ref state, ref _builtAreaVersion) | obstaclesChanged | rebuilt;
            var restampScenery = OrderChanged<Destructible>(ref state, ref _builtSceneryVersion) | rebuilt;
            if (!restamp && !restampScenery)
            {
                return;
            }

            ref var grid = ref SystemAPI.GetSingletonRW<NavGrid>().ValueRW;
            if (restamp)
            {
                grid.Version++;
                state.Dependency = new ResetJob { Base = _base, Cells = grid.Cells }.Schedule(state.Dependency);
                state.Dependency = new AreaJob { Grid = grid, Decks = false }.Schedule(_areas, state.Dependency);
                state.Dependency = new AreaJob { Grid = grid, Decks = true }.Schedule(_areas, state.Dependency);
                state.Dependency = new ObstacleJob { Grid = grid }.Schedule(_obstacles, state.Dependency);
                state.Dependency = new ClearanceJob { Grid = grid }.Schedule(state.Dependency);
            }

            if (restampScenery)
            {
                state.Dependency = new ClearSceneryJob { Scenery = grid.Scenery }.Schedule(state.Dependency);
                state.Dependency = new SceneryJob { Grid = grid }.Schedule(_scenery, state.Dependency);
            }
        }

        /// <summary>Whether the grid was built from this map and terrain; a new map or late-streamed terrain rebuilds it.</summary>
        private readonly bool SameGround(in MapSettings map, in TerrainHeight terrain)
        {
            if (!terrain.Blob.Equals(_builtTerrain.Blob))
            {
                return false;
            }

            var built = _builtMap;
            if (!map.SameArea(built) || map.NavCellSize != built.NavCellSize)
            {
                return false;
            }

            var sameWater = map.WaterLevel == built.WaterLevel && map.FloodTerrain == built.FloodTerrain;
            return sameWater && map.MaxSlope == built.MaxSlope;
        }

        /// <summary>True when entities with <typeparamref name="T"/> were added, removed or changed archetype since the last stamp.</summary>
        private static bool OrderChanged<T>(ref SystemState state, ref int built) where T : unmanaged, IComponentData
        {
            var version = state.EntityManager.GetComponentOrderVersion<T>();
            var changed = version != built;
            built = version;
            return changed;
        }

        /// <summary>Replaces the grid (keeping its version count, so paths revalidate) and classifies its base cells.</summary>
        private void BuildGrid(ref SystemState state, in MapSettings map, in TerrainHeight terrain)
        {
            var cellSize = math.max(0.1f, map.NavCellSize);
            var size = math.max(1, (int2)math.ceil(map.Size / cellSize));
            var count = size.x * size.y;
            var grid = NavGrid.Create(size, map.Min, cellSize, terrain, map.WaterLevel);
            ReplaceGrid(ref state, ref grid);

            if (_base.IsCreated)
            {
                _base.Dispose();
            }

            _base = new NativeArray<byte>(count, Allocator.Persistent);
            state.Dependency = new ClassifyJob
            {
                Grid = grid,
                Base = _base,
                Flood = map.FloodTerrain,
                MaxGradient = NavGround.MaxGradient(map.MaxSlope),
            }.Schedule(count, 1024, state.Dependency);
        }

        private void ReplaceGrid(ref SystemState state, ref NavGrid grid)
        {
            if (!SystemAPI.TryGetSingletonRW<NavGrid>(out var current))
            {
                state.EntityManager.CreateSingleton(grid, "NavGrid");
                return;
            }

            // Every job reading the old cells must finish before they are freed.
            state.CompleteDependency();
            grid.Version = current.ValueRO.Version;
            current.ValueRW.Dispose();
            current.ValueRW = grid;
        }

        /// <summary>Gives each cell the surface the ground alone makes (<see cref="NavGround"/>).</summary>
        [BurstCompile]
        private struct ClassifyJob : IJobParallelFor
        {
            [ReadOnly] public NavGrid Grid;
            public NativeArray<byte> Base;
            public bool Flood;
            public float MaxGradient;

            public void Execute(int index)
            {
                var center = Grid.CellCenter(Grid.Cell(index)).xz;
                Base[index] = (byte)NavGround.Classify(Grid.Terrain, center, Grid.CellSize, Grid.WaterLevel, Flood,
                    MaxGradient);
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

                var box = new NavBox(transform.Position, transform.Rotation, area.Size);
                Grid.GetBoxArea(box, out var min, out var max);
                for (var y = min.y; y <= max.y; y++)
                {
                    for (var x = min.x; x <= max.x; x++)
                    {
                        if (!Grid.InBox(box, new int2(x, y)))
                        {
                            continue;
                        }

                        var index = Grid.Index(new int2(x, y));
                        Grid.Cells[index] = (byte)NavGround.Stamp(area.Kind, (NavSurface)Grid.Cells[index]);
                        if (Decks)
                        {
                            Grid.DeckHeights[index] = transform.Position.y;
                        }
                    }
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
                var box = new NavBox(transform.Position, transform.Rotation, obstacle.Size);
                Grid.GetBoxArea(box, out var min, out var max);
                for (var y = min.y; y <= max.y; y++)
                {
                    for (var x = min.x; x <= max.x; x++)
                    {
                        if (Grid.InBox(box, new int2(x, y)))
                        {
                            Grid.Cells[Grid.Index(new int2(x, y))] = (byte)NavSurface.Blocked;
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private struct ClearanceJob : IJob
        {
            public NavGrid Grid;

            public void Execute() => Grid.BuildClearance();
        }

        [BurstCompile]
        private struct ClearSceneryJob : IJob
        {
            public NativeArray<byte> Scenery;

            public void Execute()
            {
                for (var i = 0; i < Scenery.Length; i++)
                {
                    Scenery[i] = 0;
                }
            }
        }

        /// <summary>Single-threaded: scenery sharing a cell keeps the sturdiest rating. Each piece fills its own cell.</summary>
        [BurstCompile]
        private partial struct SceneryJob : IJobEntity
        {
            public NavGrid Grid;

            private void Execute(in Destructible destructible, in LocalTransform transform)
            {
                var cell = Grid.WorldToCell(transform.Position);
                if (destructible.Crush <= 0 || !Grid.InBounds(cell))
                {
                    return;
                }

                var index = Grid.Index(cell);
                var rating = math.min(destructible.Crush, NavAccess.AllScenery - 1);
                Grid.Scenery[index] = (byte)math.max((int)Grid.Scenery[index], rating);
            }
        }
    }
}
