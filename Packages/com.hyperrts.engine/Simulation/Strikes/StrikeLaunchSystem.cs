using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Strikes
{
    /// <summary>
    /// Launches each <see cref="StrikeAbility"/> fired this frame: a <see cref="BombingFlight"/> for a bomber strike,
    /// a missile, its launch cue and its warning for a missile strike.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(StrategicAssetSystem))]
    public partial struct StrikeLaunchSystem : ISystem
    {
        private PrefabSpawner _spawner;
        private CueWriter _cues;
        private StrikeDefences _defences;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<AbilityEvents>();
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
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var strikeLists = SystemAPI.GetBufferLookup<StrikeAbility>(true);
            foreach (var used in SystemAPI.GetSingletonBuffer<AbilityActivation>(true))
            {
                if (!strikeLists.TryGetBuffer(used.Caster, out var strikes))
                {
                    continue;
                }

                var index = StrikeAbility.Find(strikes, used.AbilityId);
                if (index < 0)
                {
                    continue;
                }

                if (strikes[index].Kind == StrikeKind.Bomber)
                {
                    SendBomber(ref state, used, SystemAPI.GetComponent<BomberStrike>(used.Caster), ecb);
                }
                else
                {
                    LaunchMissile(ref state, used, SystemAPI.GetComponent<MissileStrike>(used.Caster), ecb);
                }
            }
        }

        private void SendBomber(ref SystemState state, in AbilityActivation used, in BomberStrike strike,
            EntityCommandBuffer ecb)
        {
            var players = SystemAPI.GetSingleton<PlayerRegistry>();
            var start = SystemAPI.GetComponent<PlayerSpawn>(players[used.Owner]).Position;
            var route = DropRoute.Plan(SystemAPI.GetSingleton<MapSettings>(), start, used.Position, strike.Altitude);
            var flight = SystemAPI.GetComponent<Flight>(strike.Aircraft);
            var aircraft = route.Launch(ecb, _spawner, strike.Aircraft, flight, strike.Altitude, used.Owner);
            ecb.AddComponent(aircraft, new BombingFlight
            {
                RevealSeconds = strike.RevealSeconds,
                Bombs = strike.Bombs,
                ImpactTypeId = _cues.TypeIdFor(used.Caster, CueSlot.Impact),
                WarningTypeId = _cues.TypeIdFor(used.Caster, CueSlot.Warning),
            });
        }

        /// <summary>Defences are counted at launch, so the warning shows the blast that lands.</summary>
        private void LaunchMissile(ref SystemState state, in AbilityActivation used, in MissileStrike strike,
            EntityCommandBuffer ecb)
        {
            var relations = SystemAPI.GetSingleton<OwnerRelations>();
            var defences = _defences.Count(ref state, used.Position, used.Owner, relations, ProjectileKind.Missile);
            var blast = strike.BlastAfter(defences);
            var structure = SystemAPI.GetComponent<LocalTransform>(used.Caster);
            var start = structure.TransformPoint(strike.LaunchPoint);
            var course = ShotCourse.Lofted;
            var missile = _spawner.Spawn(ecb, strike.Missile, start, quaternion.LookRotationSafe(math.up(), math.forward()),
                used.Owner);
            ecb.AddComponent(missile, new Projectile
            {
                Speed = course.PathLength(start, used.Position, strike.Apex) / strike.FlightSeconds,
                ImpactTypeId = _cues.TypeIdFor(used.Caster, CueSlot.Impact),
                Hit = blast.Hit(used.Caster, used.Owner, start, Entity.Null, used.Position, WeaponTargets.Surface),
                Course = course,
                Start = start,
                Apex = strike.Apex,
            });
            _cues.Play(used.Caster, CueSlot.Fire, structure, math.up(), used.Owner);
            if (strike.Remains != Entity.Null)
            {
                // Defences shrink the cleared ground with the blast.
                var clear = strike.ClearRadius * blast.SplashRadius / strike.Blast.SplashRadius;
                ecb.AddComponent(missile, new StrikeRemains { Prefab = strike.Remains, ClearRadius = clear });
            }

            _cues.Warn(_cues.TypeIdFor(used.Caster, CueSlot.Warning), used.Owner, used.Position, blast.SplashRadius);
        }
    }
}
