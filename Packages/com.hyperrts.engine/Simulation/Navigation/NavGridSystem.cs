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
    /// Builds the <see cref="NavGrid"/> from <see cref="MapSettings"/> and <see cref="TerrainHeight"/>, then re-stamps
    /// every <see cref="NavArea"/>, <see cref="NavObstacle"/> and enabled <see cref="Blocker"/> when areas or
    /// footprints change, records where cells changed and rebuilds clearance and regions. Destructible scenery has its
    /// own layer, which changes without bumping the version. None of them are assumed to move.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(MovementSystemGroup), OrderFirst = true)]
    public partial struct NavGridSystem : ISystem
    {
        private EntityQuery _obstacles;
        private EntityQuery _areas;
        private EntityQuery _scenery;
        private EntityQuery _blockers;
        private NativeArray<byte> _base;
        private MapSettings _builtMap;
        private TerrainHeight _builtTerrain;
        private int _builtObstacleVersion;
        private int _builtBlockerVersion;
        private int _builtBlockerCount;
        private ulong _builtFootprintSignature;
        private int _builtAreaVersion;
        private int _builtSceneryVersion;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _obstacles = SystemAPI.QueryBuilder().WithAll<NavObstacle, LocalTransform>().Build();
            _areas = SystemAPI.QueryBuilder().WithAll<NavArea, LocalTransform>().Build();
            _scenery = SystemAPI.QueryBuilder().WithAll<Destructible, LocalTransform>()
                .WithNone<NavObstacle, NavAgent>().Build();
            _blockers = SystemAPI.QueryBuilder().WithAll<Blocker, LocalTransform>().Build();
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

            var footprintsChanged = FootprintsMayDiffer(ref state) && SignatureChanged(ref state);
            var restamp = OrderChanged<NavArea>(ref state, ref _builtAreaVersion) | footprintsChanged | rebuilt;
            var restampScenery = OrderChanged<Destructible>(ref state, ref _builtSceneryVersion) | rebuilt;
            if (!restamp && !restampScenery)
            {
                return;
            }

            ref var grid = ref SystemAPI.GetSingletonRW<NavGrid>().ValueRW;
            if (restamp)
            {
                Restamp(ref state, ref grid, rebuilt);
            }

            if (restampScenery)
            {
                state.Dependency = new ClearSceneryJob { Scenery = grid.Scenery }
                    .Schedule(grid.Scenery.Length, 4096, state.Dependency);
                state.Dependency = new SceneryJob { Grid = grid }.Schedule(_scenery, state.Dependency);
            }
        }

        private void Restamp(ref SystemState state, ref NavGrid grid, bool rebuilt)
        {
            grid.Version++;
            if (rebuilt)
            {
                grid.Changes.Since = grid.Version + 1;
            }

            var dependency = new ResetJob { Base = _base, Cells = grid.Cells, Previous = grid.Changes.Previous }
                .Schedule(state.Dependency);
            dependency = new AreaJob { Grid = grid, Decks = false }.Schedule(_areas, dependency);
            dependency = new AreaJob { Grid = grid, Decks = true }.Schedule(_areas, dependency);
            dependency = new ObstacleJob { Grid = grid }.Schedule(_obstacles, dependency);
            dependency = new BlockerJob { Grid = grid }.Schedule(_blockers, dependency);
            dependency = new ChangeJob { Grid = grid }.Schedule(dependency);
            state.Dependency = grid.ScheduleAccess(dependency);
        }

        // Footprints only differ after an add, removal or archetype change, or a blocker turning off as it sinks.
        private bool FootprintsMayDiffer(ref SystemState state)
        {
            var count = _blockers.CalculateEntityCount();
            var toggled = count != _builtBlockerCount;
            _builtBlockerCount = count;
            return OrderChanged<NavObstacle>(ref state, ref _builtObstacleVersion) |
                   OrderChanged<Blocker>(ref state, ref _builtBlockerVersion) | toggled;
        }

        /// <summary>Whether the grid was built from this map and terrain; a new map or late-streamed terrain rebuilds it.</summary>
        private readonly bool SameGround(in MapSettings map, in TerrainHeight terrain)
        {
            var built = _builtMap;
            var sameArea = map.SameArea(built) && map.NavCellSize == built.NavCellSize;
            var sameWater = map.WaterLevel == built.WaterLevel && map.FloodTerrain == built.FloodTerrain;
            return terrain.Blob.Equals(_builtTerrain.Blob) && sameArea && sameWater && map.MaxSlope == built.MaxSlope;
        }

        /// <summary>True when entities with <typeparamref name="T"/> were added, removed or changed archetype since the last stamp.</summary>
        private static bool OrderChanged<T>(ref SystemState state, ref int built) where T : unmanaged, IComponentData
        {
            var version = state.EntityManager.GetComponentOrderVersion<T>();
            var changed = version != built;
            built = version;
            return changed;
        }

        /// <summary>Whether obstacle and blocker footprints differ from the last stamp, so fog tags alone cost no rebuild.</summary>
        private bool SignatureChanged(ref SystemState state)
        {
            // An order-independent sum, since an archetype move reorders the chunks.
            var signature = (ulong)_obstacles.CalculateEntityCount() + ((ulong)_builtBlockerCount << 32);
            foreach (var (obstacle, transform) in SystemAPI.Query<RefRO<NavObstacle>, RefRO<LocalTransform>>())
            {
                signature += obstacle.ValueRO.Hash(transform.ValueRO);
            }

            foreach (var (blocker, transform) in SystemAPI.Query<RefRO<Blocker>, RefRO<LocalTransform>>())
            {
                signature += blocker.ValueRO.Hash(transform.ValueRO);
            }

            var changed = signature != _builtFootprintSignature;
            _builtFootprintSignature = signature;
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
            var classify = new ClassifyJob
            {
                Grid = grid, Base = _base, Flood = map.FloodTerrain, MaxGradient = NavGround.MaxGradient(map.MaxSlope),
            };
            state.Dependency = classify.Schedule(count, 1024, state.Dependency);
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

        [BurstCompile]
        private struct ClassifyJob : IJobParallelFor
        {
            [ReadOnly] public NavGrid Grid;
            public NativeArray<byte> Base;
            public bool Flood;
            public float MaxGradient;

            public void Execute(int index) => Base[index] = (byte)Grid.GroundSurface(index, Flood, MaxGradient);
        }

        /// <summary>Keeps the last stamp to compare with, then starts again from the ground.</summary>
        [BurstCompile]
        private struct ResetJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Base;
            public NativeArray<byte> Cells;
            public NativeArray<byte> Previous;

            public void Execute()
            {
                Previous.CopyFrom(Cells);
                Cells.CopyFrom(Base);
            }
        }

        [BurstCompile]
        private struct ChangeJob : IJob
        {
            public NavGrid Grid;

            public void Execute() => Grid.Changes.Record(Grid);
        }

        /// <summary>Single-threaded: overlapping areas write the same cells. Decks go last so they span water areas.</summary>
        [BurstCompile]
        private partial struct AreaJob : IJobEntity
        {
            public NavGrid Grid;
            public bool Decks;

            private void Execute(in NavArea area, in LocalTransform transform)
            {
                if ((area.Kind == NavAreaKind.Deck) == Decks)
                {
                    var box = new NavBox(transform.Position, transform.Rotation, area.Size);
                    Grid.StampArea(box, area.Kind, transform.Position.y);
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
                Grid.StampArea(box, NavAreaKind.Blocked, 0f);
            }
        }

        /// <summary>Single-threaded: blockers may share cells. Disabled ones are skipped.</summary>
        [BurstCompile]
        private partial struct BlockerJob : IJobEntity
        {
            public NavGrid Grid;

            private void Execute(in Blocker blocker, in LocalTransform transform) => blocker.Stamp(ref Grid, transform);
        }

        [BurstCompile]
        private struct ClearSceneryJob : IJobParallelFor
        {
            public NativeArray<byte> Scenery;

            public void Execute(int index) => Scenery[index] = 0;
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
