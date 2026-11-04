using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Builds <see cref="CoverSpots"/> and builds it again only when the set of entities with <see cref="CoverSpot"/>s,
    /// their places or their spots change, then frees the spots nobody reserved last frame.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateBefore(typeof(SquadMemberSystem))]
    public partial struct CoverSpotSystem : ISystem
    {
        private EntityQuery _cover;
        private EntityQuery _changed;
        private MapSettings _builtMap;
        private int _builtCount;
        private ulong _builtSignature;
        private int _builds;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Spots of a site still being built do not exist yet; its enabled bit leaves the query as it completes.
            _cover = SystemAPI.QueryBuilder().WithAll<CoverSpot, LocalTransform>().WithNone<ConstructionProgress>().Build();
            // Its own description, so the change filter isn't shared with the cached query above.
            _changed = SystemAPI.QueryBuilder().WithAll<CoverSpot, LocalTransform>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
            _changed.SetChangedVersionFilter(ComponentType.ReadOnly<CoverSpot>());
            _changed.AddChangedVersionFilter(ComponentType.ReadOnly<LocalTransform>());
            state.RequireForUpdate<MapSettings>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (SystemAPI.TryGetSingleton<CoverSpots>(out var spots))
            {
                spots.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var map = SystemAPI.GetSingleton<MapSettings>();
            var missing = !SystemAPI.HasSingleton<CoverSpots>() || !map.SameArea(_builtMap);
            var count = _cover.CalculateEntityCount();
            var differ = missing || count != _builtCount || !_changed.IsEmpty;
            if (differ && (SignatureChanged(ref state, count) || missing))
            {
                Rebuild(ref state, map);
            }

            _builtMap = map;
            _builtCount = count;
            ExpireReservations(ref state);
        }

        private void ExpireReservations(ref SystemState state)
        {
            var reservations = SystemAPI.GetSingletonRW<CoverSpots>().ValueRW.Reservations;
            for (var i = 0; i < reservations.Length; i++)
            {
                var reservation = reservations[i];
                reservation.Expire();
                reservations[i] = reservation;
            }
        }

        private bool SignatureChanged(ref SystemState state, int count)
        {
            // An order-independent sum, since an archetype move reorders the chunks.
            var signature = (ulong)count;
            foreach (var (spots, transform) in SystemAPI.Query<DynamicBuffer<CoverSpot>, RefRO<LocalTransform>>()
                         .WithNone<ConstructionProgress>())
            {
                signature += math.hash(transform.ValueRO.Position) + (ulong)math.hash(transform.ValueRO.Rotation.value) * 31UL
                    + (ulong)spots.Length;
                foreach (var spot in spots)
                {
                    signature += spot.Hash();
                }
            }

            var changed = signature != _builtSignature;
            _builtSignature = signature;
            return changed;
        }

        private void Rebuild(ref SystemState state, in MapSettings map)
        {
            var world = new NativeList<WorldCoverSpot>(64, Allocator.Temp);
            foreach (var (spots, transform, entity) in
                     SystemAPI.Query<DynamicBuffer<CoverSpot>, RefRO<LocalTransform>>()
                         .WithNone<ConstructionProgress>().WithEntityAccess())
            {
                for (var i = 0; i < spots.Length; i++)
                {
                    world.Add(World(spots[i], transform.ValueRO, entity, (byte)i));
                }
            }

            _builds++;
            var built = CoverSpots.Create(map, world.AsArray(), _builds);
            if (SystemAPI.HasSingleton<CoverSpots>())
            {
                state.CompleteDependency();
                ref var current = ref SystemAPI.GetSingletonRW<CoverSpots>().ValueRW;
                current.Dispose();
                current = built;
            }
            else
            {
                state.EntityManager.CreateSingleton(built, "CoverSpots");
            }
        }

        private static WorldCoverSpot World(in CoverSpot spot, in LocalTransform transform, Entity entity, byte index)
        {
            var facing = math.rotate(transform.Rotation, new float3(spot.Facing.x, 0f, spot.Facing.y)).xz;
            var position = transform.TransformPoint(spot.Position);
            return new WorldCoverSpot
            {
                Position = position,
                Drop = math.max(0f, transform.Position.y - position.y),
                Facing = math.normalizesafe(facing),
                Kind = spot.Kind,
                Entity = entity,
                Index = index,
            };
        }
    }
}
