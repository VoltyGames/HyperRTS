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
    /// Builds the <see cref="NavGrid"/> from <see cref="MapSettings"/> and <see cref="TerrainHeight"/>, and re-stamps it
    /// when areas, footprints or scenery change, none of which are assumed to move.
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
        private NativeArray<byte> _previousScenery;
        private MapSettings _builtMap;
        private TerrainHeight _builtTerrain;
        private int _builtObstacleVersion;
        private int _builtBlockerVersion;
        private int _builtBlockerCount;
        private ulong _builtFootprintSignature;
        private int _builtAreaVersion;
        private int _builtSceneryVersion;
        private ulong _builtScenerySignature;

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

            var footprintsChanged = FootprintsMayDiffer(ref state) && SignatureChanged(ref state);
            var restamp = OrderChanged<NavArea>(ref state, ref _builtAreaVersion) | footprintsChanged | rebuilt;
            // Unit spawns and deaths bump the order too, so only a changed set of scenery refills it.
            var sceneryMayDiffer = OrderChanged<Destructible>(ref state, ref _builtSceneryVersion);
            var restampScenery = (sceneryMayDiffer && ScenerySignatureChanged(ref state)) | rebuilt;
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
            dependency = new AreaJob { Grid = grid, Decks = false }.Schedule(_areas, dependency);
            dependency = new AreaJob { Grid = grid, Decks = true }.Schedule(_areas, dependency);
            dependency = new ObstacleJob { Grid = grid }.Schedule(_obstacles, dependency);
            dependency = new BlockerJob { Grid = grid }.Schedule(_blockers, dependency);
            state.Dependency = new ChangeJob { Grid = grid }.Schedule(dependency);
        }

        // Footprints only differ after an add, removal or archetype change, or a blocker turning off as it is buried.
        private bool FootprintsMayDiffer(ref SystemState state)
        {
            var count = _blockers.CalculateEntityCount();
            var toggled = count != _builtBlockerCount;
            _builtBlockerCount = count;
            return OrderChanged<NavObstacle>(ref state, ref _builtObstacleVersion) |
                   OrderChanged<Blocker>(ref state, ref _builtBlockerVersion) | toggled;
        }

        /// <summary>Whether the scenery's count, places or ratings differ from the last fill.</summary>
        private bool ScenerySignatureChanged(ref SystemState state)
        {
            // An order-independent sum, since an archetype move reorders the chunks.
            var signature = (ulong)_scenery.CalculateEntityCount();
            foreach (var (destructible, transform) in SystemAPI.Query<RefRO<Destructible>, RefRO<LocalTransform>>()
                         .WithNone<NavObstacle, NavAgent>())
            {
                signature += math.hash(new float4(transform.ValueRO.Position, destructible.ValueRO.Crush));
            }

            var changed = signature != _builtScenerySignature;
            _builtScenerySignature = signature;
            return changed;
        }

        /// <summary>Whether the grid was built from this map and terrain; a new map or late-streamed terrain rebuilds it.</summary>
        private readonly bool SameGround(in MapSettings map, in TerrainHeight terrain)
        {
            var built = _builtMap;
            var sameArea = map.SameNavGrid(built);
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

        /// <summary>
        /// Replaces the grid (keeping its version count, so paths revalidate, and its movement classes) and classifies
        /// its base cells.
        /// </summary>
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
            // Swapped, so the old grid frees the new one's empty registry and the classes live on.
            (grid.Classes, current.ValueRW.Classes) = (current.ValueRO.Classes, grid.Classes);
            grid.FitAccess();
            current.ValueRW.Dispose();
            current.ValueRW = grid;
        }
    }
}
