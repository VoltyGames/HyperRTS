using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>Resolves the frame's <see cref="DamageEvent"/>s in order. Heals skip every multiplier.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(SectionCollapseSystem))]
    public partial struct DamageSystem : ISystem
    {
        private DamageFactors _factors;
        private EventWriter<EntityDamaged> _damaged;
        private SectionDamage _buildings;

        public void OnCreate(ref SystemState state)
        {
            _factors = new DamageFactors(ref state);
            _damaged = new EventWriter<EntityDamaged>(ref state);
            _buildings = new SectionDamage(ref state);

            EventQueues.Create<DamageQueue>(ref state, typeof(DamageEvent));
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _factors.Update(ref state);
            _damaged.Update(ref state);
            _buildings.Update(ref state);

            new ResolveJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Health = SystemAPI.GetComponentLookup<Health>(false),
                Attackers = SystemAPI.GetComponentLookup<LastAttacker>(false),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Factors = _factors,
                Destructibles = SystemAPI.GetComponentLookup<Destructible>(true),
                LethalHits = SystemAPI.GetComponentLookup<LethalHit>(false),
                Factions = SystemAPI.GetComponentLookup<Faction>(true),
                Damaged = _damaged,
                Shelters = SystemAPI.GetComponentLookup<Shelter>(true),
                ShelterHits = SystemAPI.GetBufferLookup<ShelterHit>(false),
                Buildings = _buildings,
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(DamageQueue))]
        private partial struct ResolveJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public FactionRelations Relations;
            public ComponentLookup<Health> Health;
            public ComponentLookup<LastAttacker> Attackers;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public DamageFactors Factors;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            public ComponentLookup<LethalHit> LethalHits;
            [ReadOnly] public ComponentLookup<Faction> Factions;
            public EventWriter<EntityDamaged> Damaged;
            [ReadOnly] public ComponentLookup<Shelter> Shelters;
            public BufferLookup<ShelterHit> ShelterHits;
            public SectionDamage Buildings;

            private void Execute(DynamicBuffer<DamageEvent> events)
            {
                var victims = new NativeList<SpatialEntry>(16, Allocator.Temp);
                foreach (var hit in events)
                {
                    Apply(hit.Target, hit.Amount, hit, hit.Origin);
                    if (hit.Radius > 0f)
                    {
                        Splash(hit, victims);
                    }
                }

                events.Clear();
            }

            private void Splash(in DamageEvent hit, NativeList<SpatialEntry> victims)
            {
                victims.Clear();
                var collector = new SplashCollector { Victims = victims };
                Index.Query(hit.Position, hit.Radius, ref collector);

                foreach (var victim in victims)
                {
                    if (victim.Entity == hit.Target || !SplashReaches(hit, victim))
                    {
                        continue;
                    }

                    var gap = EntityRadius.EdgeDistance(victim.Position, victim.Radius, hit.Position, 0f);
                    var amount = hit.Amount * hit.SplashFactor(gap);
                    Apply(victim.Entity, amount, hit, hit.Position);
                }
            }

            /// <summary>Splash stays on the layers the hit reaches, so a ground burst spares aircraft above. Heals reach allies only.</summary>
            private bool SplashReaches(in DamageEvent hit, in SpatialEntry victim)
            {
                if (!Weapon.CanHit(hit.Reach, victim.Layer))
                {
                    return false;
                }

                if (hit.Amount < 0f)
                {
                    return Relations.IsAllied(hit.SourceFaction, victim.Faction);
                }

                return hit.FriendlyFire || Relations.IsHostile(hit.SourceFaction, victim.Faction)
                    || Destructibles.HasComponent(victim.Entity);
            }

            /// <summary>A lethal hit fells a <see cref="Topple"/> target away from <paramref name="from"/>.</summary>
            private void Apply(Entity target, float amount, in DamageEvent hit, float3 from)
            {
                if (!Health.TryGetComponent(target, out var health) || health.Current <= 0f)
                {
                    return;
                }

                if (amount < 0f)
                {
                    var restored = Buildings.Repair(target, -amount);
                    health.Current = Buildings.Resolved(target, math.min(health.Max, health.Current + restored));
                    Health[target] = health;
                    return;
                }

                PassToPassengers(target, amount, health, hit, from);
                var scale = Factors.Scale(target, hit);
                amount = Buildings.IsSectioned(target)
                    ? Buildings.Route(target, Transforms[target], hit, scale)
                    : amount * scale;
                var overkill = amount - health.Current;
                health.Current = Buildings.Resolved(target, math.max(0f, health.Current - amount));
                Health[target] = health;
                Report(target, amount);
                if (health.Current <= 0f && LethalHits.HasComponent(target))
                {
                    var heading = ToppleMath.Quantize(from, Transforms[target].Position);
                    LethalHits[target] = new LethalHit { Type = hit.Type, Overkill = overkill, Heading = heading };
                }

                if (Attackers.HasComponent(target) && hit.SourceFaction != 0)
                {
                    Attackers[target] = new LastAttacker { Source = hit.Source, Faction = hit.SourceFaction };
                }
            }

            /// <summary>Armor that soaks the whole hit raises no event.</summary>
            private void Report(Entity target, float amount)
            {
                if (amount <= 0f || !Factions.TryGetComponent(target, out var owner))
                {
                    return;
                }

                if (owner.Value != Faction.Neutral)
                {
                    Damaged.Add(new EntityDamaged { Entity = target, Faction = owner.Value });
                }
            }

            /// <summary>Passes the share of a hit on a shelter that its walls, at their health before the hit, don't stop.</summary>
            private void PassToPassengers(Entity target, float amount, in Health health, in DamageEvent hit, float3 from)
            {
                if (hit.GarrisonFactor <= 0f || !Shelters.TryGetComponent(target, out var shelter))
                {
                    return;
                }

                var passing = hit;
                passing.Target = Entity.Null;
                passing.Origin = from;
                passing.Amount = shelter.Passing(hit, amount, Buildings.Intact(target, health));
                passing.Radius = 0f;
                if (passing.Amount > 0f)
                {
                    ShelterHits[target].Add(new ShelterHit { Hit = passing });
                }
            }
        }

        private struct SplashCollector : ISpatialVisitor
        {
            public NativeList<SpatialEntry> Victims;

            public void Visit(in SpatialEntry entry) => Victims.Add(entry);
        }
    }
}
