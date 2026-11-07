using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>
    /// Builds <see cref="SightBlockers"/> on the fog grid with the map's <see cref="CropCover"/>, and restamps it when
    /// enabled <see cref="SightBlock"/>s, their places or values change. A new map, terrain or crop cover rebuilds it.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(FogOfWarSystem))]
    public partial struct SightBlockSystem : ISystem
    {
        private static readonly ProfilerMarker StampMarker = new("SightBlockSystem.Stamp");

        private EntityQuery _blocks;
        private EntityQuery _changed;
        private MapSettings _builtMap;
        private TerrainHeight _builtTerrain;
        private CropCover _builtCrops;
        private int _builtOrder;
        private int _builtCount;
        private ulong _builtSignature;

        // The last stamp, to tell which tiles a restamp changed.
        private NativeArray<SightCell> _previous;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blocks = SystemAPI.QueryBuilder().WithAll<SightBlock, LocalTransform>().Build();
            // Its own description, so the change filter isn't shared with the cached query above; it sees toggles too.
            _changed = SystemAPI.QueryBuilder().WithAll<SightBlock, LocalTransform>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
            _changed.SetChangedVersionFilter(ComponentType.ReadOnly<SightBlock>());
            _changed.AddChangedVersionFilter(ComponentType.ReadOnly<LocalTransform>());
            state.RequireForUpdate<MapSettings>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingleton<SightBlockers>(out var blockers))
            {
                blockers.Dispose();
            }

            if (_previous.IsCreated)
            {
                _previous.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var map = SystemAPI.GetSingleton<MapSettings>();
            SystemAPI.TryGetSingleton<TerrainHeight>(out var terrain);
            SystemAPI.TryGetSingleton<CropCover>(out var crops);
            var rebuilt = EnsureGrid(ref state, map, terrain, crops);
            // Checked on a rebuild too, so the set it stamps is the one later frames compare with.
            var changed = MayDiffer(ref state) && SignatureChanged(ref state);
            if (!rebuilt && !changed)
            {
                return;
            }

            ref var blockers = ref SystemAPI.GetSingletonRW<SightBlockers>().ValueRW;
            // Crop cover stays, flattened lanes and all.
            state.Dependency = new SavePreviousJob { Blockers = blockers, Previous = _previous }.Schedule(state.Dependency);
            state.Dependency = new ClearBlocksJob { Cells = blockers.Cells }
                .Schedule(blockers.Cells.Length, 4096, state.Dependency);
            state.Dependency = ClearJob<Entity>.Schedule(blockers.Owner, state.Dependency);
            state.Dependency = new StampJob { Blockers = blockers, Terrain = terrain }
                .Schedule(_blocks, state.Dependency);
            state.Dependency = new DiffJob { Blockers = blockers, Previous = _previous }.Schedule(state.Dependency);
        }

        // Blocks only differ after an add, removal or archetype change, a block turning on or off, or a write.
        private bool MayDiffer(ref SystemState state)
        {
            var order = state.EntityManager.GetComponentOrderVersion<SightBlock>();
            var count = _blocks.CalculateEntityCount();
            var differ = order != _builtOrder || count != _builtCount || !_changed.IsEmpty;
            _builtOrder = order;
            _builtCount = count;
            return differ;
        }

        private bool SignatureChanged(ref SystemState state)
        {
            // An order-independent sum, since an archetype move reorders the chunks.
            var signature = (ulong)_builtCount;
            foreach (var (block, transform) in SystemAPI.Query<RefRO<SightBlock>, RefRO<LocalTransform>>())
            {
                signature += block.ValueRO.Hash(transform.ValueRO);
            }

            var changed = signature != _builtSignature;
            _builtSignature = signature;
            return changed;
        }

        /// <summary>Creates the grid, or replaces it when the map's area, terrain or crops changed; true if it did.</summary>
        private bool EnsureGrid(ref SystemState state, in MapSettings map, in TerrainHeight terrain, in CropCover crops)
        {
            var exists = SystemAPI.HasSingleton<SightBlockers>();
            var sameGround = terrain.Blob.Equals(_builtTerrain.Blob) && crops.Blob.Equals(_builtCrops.Blob);
            if (exists && map.SameFogGrid(_builtMap) && sameGround)
            {
                return false;
            }

            _builtMap = map;
            _builtTerrain = terrain;
            _builtCrops = crops;
            var grid = SightBlockers.Create(map, Allocator.Persistent);
            if (!exists)
            {
                state.EntityManager.CreateSingleton(grid, "SightBlockers");
            }
            else
            {
                state.CompleteDependency();
                ref var current = ref SystemAPI.GetSingletonRW<SightBlockers>().ValueRW;
                grid.Generation = current.Generation + 1;
                current.Dispose();
                current = grid;
                _previous.Dispose();
            }

            _previous = new NativeArray<SightCell>(grid.Cells.Length, Allocator.Persistent);
            state.Dependency = new GroundJob { Blockers = grid, Terrain = terrain }
                .Schedule(grid.Cells.Length, 1024, state.Dependency);

            if (crops.Blob.IsCreated)
            {
                state.Dependency = new CropJob { Blockers = grid, Crops = crops }.Schedule(state.Dependency);
            }

            return true;
        }
    }
}
