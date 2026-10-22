using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Airdrops
{
    /// <summary>
    /// Flies each <see cref="BombingFlight"/>: it drops its <see cref="Stealth"/> <c>RevealSeconds</c> before the drop,
    /// releases its bombs over the target, less one per enemy anti-air weapon covering it, then flies off the map.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(StrategicAssetSystem))]
    public partial struct BombingFlightSystem : ISystem
    {
        private ComponentLookup<PostTransformMatrix> _stretchLookup;
        private CueWriter _cues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BombingFlight>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            _stretchLookup = state.GetComponentLookup<PostTransformMatrix>(true);
            _cues = new CueWriter(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _stretchLookup.Update(ref state);
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());
            var map = SystemAPI.GetSingleton<MapSettings>();
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var drops = new NativeList<Entity>(Allocator.Temp);
            foreach (var (flight, destination, transform, speed, entity) in SystemAPI
                         .Query<RefRW<BombingFlight>, RefRW<MoveDestination>, RefRO<LocalTransform>,
                             RefRO<MovementSpeed>>()
                         .WithPresent<MoveDestination>().WithNone<Dead>().WithEntityAccess())
            {
                var position = transform.ValueRO.Position;
                if (flight.ValueRO.Leaving && !map.Contains(position))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                if (!flight.ValueRO.Leaving)
                {
                    Approach(ref state, ref flight.ValueRW, entity, position, speed.ValueRO.Value, ref drops, ecb);
                }

                destination.ValueRW.Value = flight.ValueRO.Route.Destination(flight.ValueRO.Leaving);
                ecb.SetComponentEnabled<MoveDestination>(entity, true);
            }

            foreach (var bomber in drops)
            {
                Drop(ref state, bomber, ecb);
            }
        }

        private void Approach(ref SystemState state, ref BombingFlight flight, Entity entity, float3 position,
            float speed, ref NativeList<Entity> drops, EntityCommandBuffer ecb)
        {
            var distance = flight.Route.DistanceToTarget(position);
            var revealed = distance <= flight.RevealSeconds * speed;
            if (revealed && SystemAPI.HasComponent<Stealth>(entity))
            {
                ecb.SetComponentEnabled<Stealth>(entity, false);
            }

            if (flight.Route.IsOver(position))
            {
                flight.Leaving = true;
                drops.Add(entity);
            }
        }

        private void Drop(ref SystemState state, Entity bomber, EntityCommandBuffer ecb)
        {
            var flight = SystemAPI.GetComponent<BombingFlight>(bomber);
            var faction = SystemAPI.GetComponent<Faction>(bomber).Value;
            var origin = SystemAPI.GetComponent<LocalTransform>(bomber).Position;
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var load = flight.Bombs;
            var count = load.CountAfter(Defences(ref state, flight.Route.Target, faction, relations));
            var targets = Structures(ref state, flight.Route.Target, load.ZoneRadius, faction, relations);
            var impact = _cues.TypeIdFor(bomber, CueSlot.Impact);
            var prefabTransform = SystemAPI.GetComponent<LocalTransform>(load.Prefab);
            for (var i = 0; i < count; i++)
            {
                var target = targets.Length > 0 ? targets[i % targets.Length] : Entity.Null;
                var aim = target == Entity.Null
                    ? StickPoint(flight.Route, load.ZoneRadius, i, count)
                    : SystemAPI.GetComponent<LocalTransform>(target).Position;
                var bomb = ecb.Spawn(load.Prefab, prefabTransform, _stretchLookup, origin,
                    quaternion.LookRotationSafe(aim - origin, math.up()), faction);
                ecb.AddComponent(bomb, new Projectile
                {
                    Speed = load.Speed,
                    ImpactTypeId = impact,
                    Hit = Hit(load, bomber, faction, origin, target, aim),
                    Trajectory = Trajectory.Direct,
                    Start = origin,
                });
            }
        }

        private static DamageEvent Hit(in BombLoad load, Entity bomber, byte faction, float3 origin, Entity target,
            float3 aim) => new()
        {
            Target = target,
            Position = aim,
            Origin = origin,
            Source = bomber,
            SourceFaction = faction,
            Amount = load.Damage,
            Type = load.DamageType,
            Radius = load.Radius,
            EdgeFactor = load.EdgeFactor,
            Reach = WeaponTargets.Surface,
        };

        /// <summary>With no structure to seek, the bombs fall in a line along the run across the zone.</summary>
        private static float3 StickPoint(in DropRoute route, float zoneRadius, int index, int count)
        {
            var along = count > 1 ? (index / (count - 1f) * 2f - 1f) * zoneRadius : 0f;
            var heading = route.Heading;
            return route.Target + new float3(heading.x * along, 0f, heading.y * along);
        }

        /// <summary>Enemy anti-air weapons, mounts included, that are working and reach the target point.</summary>
        private int Defences(ref SystemState state, float3 point, byte faction, in FactionRelations relations)
        {
            var count = 0;
            foreach (var (weapon, transform, owner) in SystemAPI
                         .Query<RefRO<Weapon>, RefRO<LocalTransform>, RefRO<Faction>>().WithNone<Dead, Offline>())
            {
                var hostile = relations.IsHostile(faction, owner.ValueRO.Value);
                count += hostile && Covers(weapon.ValueRO, transform.ValueRO.Position, point) ? 1 : 0;
            }

            foreach (var (mounts, transform, owner) in SystemAPI
                         .Query<DynamicBuffer<WeaponMount>, RefRO<LocalTransform>, RefRO<Faction>>()
                         .WithNone<Dead, Offline>())
            {
                if (!relations.IsHostile(faction, owner.ValueRO.Value))
                {
                    continue;
                }

                foreach (var mount in mounts)
                {
                    count += Covers(mount.Weapon, transform.ValueRO.Position, point) ? 1 : 0;
                }
            }

            return count;
        }

        private static bool Covers(in Weapon weapon, float3 from, float3 point) =>
            weapon.Targets != WeaponTargets.Surface && math.distance(from.xz, point.xz) <= weapon.Range;

        /// <summary>Enemy structures in the zone, nearest the aim point first.</summary>
        private NativeList<Entity> Structures(ref SystemState state, float3 point, float radius, byte faction,
            in FactionRelations relations)
        {
            var found = new NativeList<Entity>(Allocator.Temp);
            var distances = new NativeList<float>(Allocator.Temp);
            foreach (var (transform, owner, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<Faction>>()
                         .WithAll<BuildingTag>().WithNone<Dead>().WithEntityAccess())
            {
                var distance = math.distance(transform.ValueRO.Position.xz, point.xz);
                if (distance > radius || !relations.IsHostile(faction, owner.ValueRO.Value))
                {
                    continue;
                }

                var at = 0;
                while (at < distances.Length && distances[at] <= distance)
                {
                    at++;
                }

                found.InsertRange(at, 1);
                found[at] = entity;
                distances.InsertRange(at, 1);
                distances[at] = distance;
            }

            return found;
        }
    }
}
