using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Builds <see cref="SightBlockers"/> on the fog grid and restamps it when the set of enabled
    /// <see cref="SightBlock"/>s, their places or their tops change, or the map or its terrain does.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(FogOfWarSystem))]
    public partial struct SightBlockSystem : ISystem
    {
        private EntityQuery _blocks;
        private EntityQuery _changed;
        private MapSettings _builtMap;
        private TerrainHeight _builtTerrain;
        private int _builtOrder;
        private int _builtCount;
        private ulong _builtSignature;

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
                blockers.Solid.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var map = SystemAPI.GetSingleton<MapSettings>();
            SystemAPI.TryGetSingleton<TerrainHeight>(out var terrain);
            var rebuilt = EnsureGrid(ref state, map, terrain);
            // Checked on a rebuild too, so the set it stamps is the one later frames compare with.
            var changed = MayDiffer(ref state) && SignatureChanged(ref state);
            if (!rebuilt && !changed)
            {
                return;
            }

            ref var blockers = ref SystemAPI.GetSingletonRW<SightBlockers>().ValueRW;
            blockers.Version++;
            state.Dependency = new ClearJob { Solid = blockers.Solid }.Schedule(state.Dependency);
            state.Dependency = new StampJob { Blockers = blockers, Terrain = terrain }
                .Schedule(_blocks, state.Dependency);
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

        /// <summary>Creates the grid, or replaces it when the map's area or terrain changed; true if it did.</summary>
        private bool EnsureGrid(ref SystemState state, in MapSettings map, in TerrainHeight terrain)
        {
            var exists = SystemAPI.HasSingleton<SightBlockers>();
            if (exists && map.SameFogGrid(_builtMap) && terrain.Blob.Equals(_builtTerrain.Blob))
            {
                return false;
            }

            _builtMap = map;
            _builtTerrain = terrain;
            map.FogLayout(out var size, out var cellSize);
            var grid = new SightBlockers
            {
                Solid = new NativeArray<byte>(size.x * size.y, Allocator.Persistent),
                Size = size,
                Min = map.Min,
                CellSize = cellSize,
            };
            if (!exists)
            {
                state.EntityManager.CreateSingleton(grid, "SightBlockers");
                return true;
            }

            state.CompleteDependency();
            ref var current = ref SystemAPI.GetSingletonRW<SightBlockers>().ValueRW;
            grid.Version = current.Version;
            current.Solid.Dispose();
            current = grid;
            return true;
        }

        [BurstCompile]
        private struct ClearJob : IJob
        {
            public NativeArray<byte> Solid;

            public void Execute()
            {
                for (var i = 0; i < Solid.Length; i++)
                {
                    Solid[i] = 0;
                }
            }
        }

        /// <summary>Single-threaded: overlapping blocks raise the same cells.</summary>
        [BurstCompile]
        private partial struct StampJob : IJobEntity
        {
            public SightBlockers Blockers;
            public TerrainHeight Terrain;

            private void Execute(in SightBlock block, in LocalTransform transform) =>
                Blockers.Stamp(block.Box(transform), block.Top(transform), Terrain);
        }
    }
}
