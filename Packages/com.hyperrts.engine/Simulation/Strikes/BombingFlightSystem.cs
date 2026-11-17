using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Vision.Detection;
using System;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Movement;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Strikes
{
    /// <summary>
    /// Flies each <see cref="BombingFlight"/>: it drops its <see cref="Stealth"/> and warns of the strike
    /// <c>RevealSeconds</c> before the drop, releases its bombs over the target, less one per enemy anti-air weapon
    /// covering it, then flies off the map.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(StrategicAssetSystem))]
    public partial struct BombingFlightSystem : ISystem
    {
        private PrefabSpawner _spawner;
        private CueWriter _cues;
        private StrikeDefences _defences;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BombingFlight>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            _spawner = new PrefabSpawner(ref state);
            _cues = new CueWriter(ref state);
            _defences = new StrikeDefences(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawner.Update(ref state);
            _cues.Update(ref state);
            var map = SystemAPI.GetSingleton<MapSettings>();
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var drops = new NativeList<Entity>(Allocator.Temp);
            foreach (var (flight, strike, run, destination, transform, speed, entity) in SystemAPI
                         .Query<RefRW<DropFlight>, RefRW<BombingFlight>, RefRW<DropRun>, RefRW<MoveDestination>,
                             RefRO<LocalTransform>, RefRO<MovementSpeed>>()
                         .WithPresent<MoveDestination>().WithEntityAccess())
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
                var revealed = flight.ValueRO.IsWithin(strike.ValueRO.RevealSeconds, speed.ValueRO.Value);
                if (approaching && revealed && !strike.ValueRO.Warned)
                {
                    Reveal(ref state, entity, ref strike.ValueRW, flight.ValueRO.Route.Target, ecb);
                }
            }

            foreach (var bomber in drops)
            {
                Drop(ref state, bomber, ecb);
            }
        }

        private void Reveal(ref SystemState state, Entity bomber, ref BombingFlight strike, float3 target,
            EntityCommandBuffer ecb)
        {
            strike.Warned = true;
            var owner = SystemAPI.GetComponent<Owner>(bomber).Value;
            _cues.Warn(strike.WarningTypeId, owner, target, strike.Bombs.Damage.SplashRadius + strike.Bombs.ZoneRadius);
            if (SystemAPI.HasComponent<Stealth>(bomber))
            {
                ecb.SetComponentEnabled<Stealth>(bomber, false);
            }
        }

        private void Drop(ref SystemState state, Entity bomber, EntityCommandBuffer ecb)
        {
            var route = SystemAPI.GetComponent<DropFlight>(bomber).Route;
            var strike = SystemAPI.GetComponent<BombingFlight>(bomber);
            var load = strike.Bombs;
            var owner = SystemAPI.GetComponent<Owner>(bomber).Value;
            var transform = SystemAPI.GetComponent<LocalTransform>(bomber);
            var origin = transform.TransformPoint(SystemAPI.GetComponent<DropRun>(bomber).ReleasePoint);
            var relations = SystemAPI.GetSingleton<OwnerRelations>();
            var count = load.CountAfter(_defences.Count(ref state, route.Target, owner, relations, ProjectileKind.None));
            var targets = Structures(ref state, route.Target, load.ZoneRadius, owner, relations);
            for (var i = 0; i < count; i++)
            {
                var target = targets.Length > 0 ? targets[i % targets.Length].Entity : Entity.Null;
                var aim = target == Entity.Null
                    ? route.StickPoint(load.ZoneRadius, i, count)
                    : SystemAPI.GetComponent<LocalTransform>(target).Position;
                var bomb = _spawner.Spawn(ecb, load.Prefab, origin,
                    quaternion.LookRotationSafe(aim - origin, math.up()), owner);
                ecb.AddComponent(bomb, new Projectile
                {
                    Speed = load.Speed,
                    ImpactTypeId = strike.ImpactTypeId,
                    Hit = load.Damage.Hit(bomber, owner, origin, target, aim, WeaponTargets.Surface),
                    Course = ShotCourse.Direct,
                    Start = origin,
                });
                if (load.Remains != Entity.Null)
                {
                    ecb.AddComponent(bomb, new StrikeRemains { Prefab = load.Remains });
                }
            }
        }

        /// <summary>Enemy structures in the zone, nearest the aim point first.</summary>
        private NativeList<NearStructure> Structures(ref SystemState state, float3 point, float radius, byte owner,
            in OwnerRelations relations)
        {
            var found = new NativeList<NearStructure>(Allocator.Temp);
            foreach (var (transform, targetOwner, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<Owner>>()
                         .WithAll<BuildingTag>().WithEntityAccess())
            {
                var distance = math.distance(transform.ValueRO.Position.xz, point.xz);
                if (distance <= radius && relations.IsHostile(owner, targetOwner.ValueRO.Value))
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
