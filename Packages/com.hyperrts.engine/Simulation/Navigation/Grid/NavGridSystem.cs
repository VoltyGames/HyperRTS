using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Grid
{
    /// <summary>
    /// Builds the <see cref="NavGrid"/> from <see cref="MapSettings"/> and <see cref="TerrainHeight"/>, and re-stamps it
    /// when areas, footprints or scenery change, none of which are assumed to move.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(MovementSystemGroup), OrderFirst = true)]
    public partial struct NavGridSystem : ISystem
    {
        private static readonly ProfilerMarker RestampMarker = new("NavGridSystem.Restamp");

        private EntityQuery _obstacles;
        private EntityQuery _areas;
        private EntityQuery _scenery;
        private EntityQuery _blockers;
        private EntityQuery _parts;
        private EntityQuery _areaParts;
        private NativeArray<byte> _base;
        private NativeArray<byte> _previousScenery;
        private MapSettings _builtMap;
        private TerrainHeight _builtTerrain;
        private ChangeTracker _builtObstacles;
        private ChangeTracker _builtBlockers;
        private ChangeTracker _builtAreas;
        private ChangeTracker _builtFootprints;
        private ChangeTracker _builtScenery;
        private EntityQuery _openScenery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _obstacles = SystemAPI.QueryBuilder().WithAll<NavObstacle, LocalTransform>().Build();
            _areas = SystemAPI.QueryBuilder().WithAll<NavArea, LocalTransform>().Build();
            _scenery = SystemAPI.QueryBuilder().WithAll<Destructible, LocalTransform>()
                .WithNone<NavObstacle, NavAgent>().Build();
            _openScenery = SystemAPI.QueryBuilder().WithAll<Destructible, NavObstacle, LocalTransform>()
                .WithNone<NavAgent>().Build();
            _blockers = SystemAPI.QueryBuilder().WithAll<Blocker, LocalTransform>().Build();
            _parts = SystemAPI.QueryBuilder().WithAll<NavObstaclePart, NavObstacle>().Build();
            _parts.SetChangedVersionFilter(ComponentType.ReadOnly<NavObstaclePart>());
            _areaParts = SystemAPI.QueryBuilder().WithAll<NavAreaPart, NavArea>().Build();
            _areaParts.SetChangedVersionFilter(ComponentType.ReadOnly<NavAreaPart>());
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
                _previousScenery.Dispose();
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

            var footprintsChanged = FootprintsMayDiffer(ref state) && FootprintsChanged(ref state);
            var restamp = _builtAreas.OrderChanged<NavArea>(ref state) | footprintsChanged | rebuilt;
            // Unit spawns and deaths bump the order too, so only a changed set of scenery refills it.
            var sceneryMayDiffer = _builtScenery.OrderChanged<Destructible>(ref state);
            var restampScenery = (sceneryMayDiffer && SceneryChanged(ref state)) | rebuilt;
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
                state.Dependency = ClearJob<byte>.Schedule(grid.Scenery, state.Dependency);
                state.Dependency = new SceneryJob { Grid = grid }.Schedule(_scenery, state.Dependency);
                state.Dependency = new OpenSceneryJob { Grid = grid }.Schedule(_openScenery, state.Dependency);
                state.Dependency = new SceneryDiffJob
                {
                    Field = grid.Field, Scenery = grid.Scenery, Previous = _previousScenery,
                }.Schedule(state.Dependency);
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
            var areaParts = SystemAPI.GetBufferLookup<NavAreaPart>(true);
            dependency = new AreaJob { Grid = grid, Decks = false, Parts = areaParts }.Schedule(_areas, dependency);
            dependency = new AreaJob { Grid = grid, Decks = true, Parts = areaParts }.Schedule(_areas, dependency);
            dependency = new ObstacleJob { Grid = grid, Parts = SystemAPI.GetBufferLookup<NavObstaclePart>(true) }
                .Schedule(_obstacles, dependency);
            dependency = new BlockerJob { Grid = grid }.Schedule(_blockers, dependency);
            state.Dependency = new ChangeJob { Grid = grid }.Schedule(dependency);
        }

        // Footprints only differ after an add, removal or archetype change, a blocker turning off as it is buried, or
        // a part opening or closing, on an obstacle or an area.
        private bool FootprintsMayDiffer(ref SystemState state)
        {
            return _builtObstacles.OrderChanged<NavObstacle>(ref state) |
                   _builtBlockers.OrderChanged<Blocker>(ref state) |
                   _builtBlockers.CountChanged(_blockers.CalculateEntityCount()) | !_parts.IsEmpty | !_areaParts.IsEmpty;
        }

        /// <summary>Whether the scenery's count, places or ratings differ from the last fill.</summary>
        private bool SceneryChanged(ref SystemState state)
        {
            var sum = (ulong)(_scenery.CalculateEntityCount() + _openScenery.CalculateEntityCount());
            foreach (var (destructible, transform) in SystemAPI.Query<RefRO<Destructible>, RefRO<LocalTransform>>()
                         .WithNone<NavObstacle, NavAgent>())
            {
                sum += math.hash(new float4(transform.ValueRO.Position, destructible.ValueRO.Crush));
            }

            foreach (var (destructible, obstacle, transform) in SystemAPI
                         .Query<RefRO<Destructible>, RefRO<NavObstacle>, RefRO<LocalTransform>>().WithNone<NavAgent>())
            {
                sum += obstacle.ValueRO.Open ? obstacle.ValueRO.Hash(transform.ValueRO) + (ulong)destructible.ValueRO.Crush : 0UL;
            }

            return _builtScenery.SumChanged(sum);
        }

        /// <summary>Whether the grid was built from this map and terrain; a new map or late-streamed terrain rebuilds it.</summary>
        private readonly bool SameGround(in MapSettings map, in TerrainHeight terrain)
        {
            var built = _builtMap;
            var sameArea = map.SameNavGrid(built);
            var sameWater = map.WaterLevel == built.WaterLevel && map.FloodTerrain == built.FloodTerrain;
            return terrain.Blob.Equals(_builtTerrain.Blob) && sameArea && sameWater && map.MaxSlope == built.MaxSlope;
        }

        /// <summary>Whether obstacle and blocker footprints differ from the last stamp, so fog tags alone cost no rebuild.</summary>
        private bool FootprintsChanged(ref SystemState state)
        {
            var sum = (ulong)_obstacles.CalculateEntityCount() + ((ulong)_builtBlockers.Count << 32);
            var parts = SystemAPI.GetBufferLookup<NavObstaclePart>(true);
            foreach (var (obstacle, transform, entity) in SystemAPI.Query<RefRO<NavObstacle>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                sum += obstacle.ValueRO.Hash(transform.ValueRO);
                if (parts.TryGetBuffer(entity, out var pieces))
                {
                    foreach (var part in pieces)
                    {
                        sum += part.Closed ? math.hash(new float4(part.Center, part.Size)) : 0u;
                    }
                }
            }

            var areaParts = SystemAPI.GetBufferLookup<NavAreaPart>(true);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<NavArea>>().WithAll<NavAreaPart, LocalTransform>()
                         .WithEntityAccess())
            {
                foreach (var part in areaParts[entity])
                {
                    sum += part.Open ? math.hash(new float4(part.Center, part.Size)) : 0u;
                }
            }

            foreach (var (blocker, transform) in SystemAPI.Query<RefRO<Blocker>, RefRO<LocalTransform>>())
            {
                sum += blocker.ValueRO.Hash(transform.ValueRO);
            }

            return _builtFootprints.SumChanged(sum);
        }

        /// <summary>
        /// Replaces the grid (keeping its version count, so paths revalidate, and its movement classes) and classifies
        /// its base cells.
        /// </summary>
        private void BuildGrid(ref SystemState state, in MapSettings map, in TerrainHeight terrain)
        {
            var cellGrid = map.NavGrid();
            var count = cellGrid.Count;
            var grid = NavGrid.Create(cellGrid, terrain, map.WaterLevel);
            state.ReplaceSingleton(ref grid, "NavGrid");

            if (_base.IsCreated)
            {
                _base.Dispose();
                _previousScenery.Dispose();
            }

            _base = new NativeArray<byte>(count, Allocator.Persistent);
            _previousScenery = new NativeArray<byte>(count, Allocator.Persistent);
            var classify = new ClassifyJob
            {
                Grid = grid, Base = _base, Flood = map.FloodTerrain, MaxGradient = NavGround.MaxGradient(map.MaxSlope),
            };
            state.Dependency = classify.Schedule(count, 1024, state.Dependency);
        }
    }
}
