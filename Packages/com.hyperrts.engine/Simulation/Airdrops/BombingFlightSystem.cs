using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Air;
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
            foreach (var (flight, strike, run, destination, transform, speed, entity) in SystemAPI
                         .Query<RefRW<DropFlight>, RefRO<BombingFlight>, RefRW<DropRun>, RefRW<MoveDestination>,
                             RefRO<LocalTransform>, RefRO<MovementSpeed>>()
                         .WithPresent<MoveDestination>().WithNone<Dead>().WithEntityAccess())
            {
                if (flight.ValueRO.HasLeft(map, transform.ValueRO.Position))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var approaching = !flight.ValueRO.Leaving;
                if (flight.ValueRW.Fly(transform.ValueRO, speed.ValueRO.Value, ref run.ValueRW, ref destination.ValueRW,
                        out _))
                {
                    drops.Add(entity);
                }

                ecb.SetComponentEnabled<MoveDestination>(entity, true);
                var reveal = strike.ValueRO.RevealSeconds;
                if (approaching && flight.ValueRO.IsWithin(reveal, speed.ValueRO.Value)
                    && SystemAPI.HasComponent<Stealth>(entity))
                {
                    ecb.SetComponentEnabled<Stealth>(entity, false);
                }
            }

            foreach (var bomber in drops)
            {
                Drop(ref state, bomber, ecb);
            }
        }

        private void Drop(ref SystemState state, Entity bomber, EntityCommandBuffer ecb)
        {
            var route = SystemAPI.GetComponent<DropFlight>(bomber).Route;
            var load = SystemAPI.GetComponent<BombingFlight>(bomber).Bombs;
            var faction = SystemAPI.GetComponent<Faction>(bomber).Value;
            var transform = SystemAPI.GetComponent<LocalTransform>(bomber);
            var origin = transform.TransformPoint(SystemAPI.GetComponent<DropRun>(bomber).ReleasePoint);
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var count = load.CountAfter(Defences(ref state, route.Target, faction, relations));
            var targets = Structures(ref state, route.Target, load.ZoneRadius, faction, relations);
            var impact = _cues.TypeIdFor(bomber, CueSlot.Impact);
            var prefabTransform = SystemAPI.GetComponent<LocalTransform>(load.Prefab);
            for (var i = 0; i < count; i++)
            {
                var target = targets.Length > 0 ? targets[i % targets.Length].Entity : Entity.Null;
                var aim = target == Entity.Null
                    ? route.StickPoint(load.ZoneRadius, i, count)
                    : SystemAPI.GetComponent<LocalTransform>(target).Position;
                var bomb = ecb.Spawn(load.Prefab, prefabTransform, _stretchLookup, origin,
                    quaternion.LookRotationSafe(aim - origin, math.up()), faction);
                ecb.AddComponent(bomb, new Projectile
                {
                    Speed = load.Speed,
                    ImpactTypeId = impact,
                    Hit = load.Hit(bomber, faction, origin, target, aim),
                    Trajectory = Trajectory.Direct,
                    Start = origin,
                });
            }
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
            CombatMath.CanHit(weapon.Targets, NavLayer.Air) && weapon.Reaches(math.distance(from.xz, point.xz));

        /// <summary>Enemy structures in the zone, nearest the aim point first.</summary>
        private NativeList<NearStructure> Structures(ref SystemState state, float3 point, float radius, byte faction,
            in FactionRelations relations)
        {
            var found = new NativeList<NearStructure>(Allocator.Temp);
            foreach (var (transform, owner, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<Faction>>()
                         .WithAll<BuildingTag>().WithNone<Dead>().WithEntityAccess())
            {
                var distance = math.distance(transform.ValueRO.Position.xz, point.xz);
                if (distance <= radius && relations.IsHostile(faction, owner.ValueRO.Value))
                {
                    found.Add(new NearStructure { Distance = distance, Entity = entity });
                }
            }

            found.Sort();
            return found;
        }

        private struct NearStructure : IComparable<NearStructure>
        {
            public float Distance;
            public Entity Entity;

            // Ties fall back to the entity, so every peer picks the same order.
            public int CompareTo(NearStructure other) =>
                Distance != other.Distance ? Distance.CompareTo(other.Distance) : Entity.CompareTo(other.Entity);
        }
    }
}
