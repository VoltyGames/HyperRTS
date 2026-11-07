using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Death;
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
    /// <summary>
    /// Resolves the frame's <see cref="DamageEvent"/>s in order: heals skip every multiplier, and squad hits land on
    /// members by seed, splash and cover.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(SectionCollapseSystem))]
    public partial struct DamageSystem : ISystem
    {
        private DamageFactors _factors;
        private EventWriter<EntityDamaged> _damaged;
        private SectionDamage _buildings;
        private SquadDamage _squads;

        public void OnCreate(ref SystemState state)
        {
            _factors = new DamageFactors(ref state);
            _damaged = new EventWriter<EntityDamaged>(ref state);
            _buildings = new SectionDamage(ref state);
            _squads = new SquadDamage(ref state);

            EventQueues.Create<DamageQueue>(ref state, typeof(DamageEvent));
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<SimulationFrame>();
            state.RequireForUpdate<CoverRules>();
            state.RequireForUpdate<SquadRules>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _factors.Update(ref state);
            _damaged.Update(ref state);
            _buildings.Update(ref state);
            _squads.Update(ref state, SystemAPI.GetSingleton<CoverRules>());

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
                Squads = _squads,
                Frame = SystemAPI.GetSingleton<SimulationFrame>().Value,
                Squad = SystemAPI.GetSingleton<SquadRules>(),
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
            public SquadDamage Squads;
            public uint Frame;
            public SquadRules Squad;

            private void Execute(DynamicBuffer<DamageEvent> events)
            {
                var victims = new NativeList<SpatialEntry>(16, Allocator.Temp);
                for (var i = 0; i < events.Length; i++)
                {
                    var hit = events[i];
                    var struck = Apply(hit.Target, hit, new Blow
                    {
                        Amount = hit.Profile.Amount,
                        From = hit.Origin,
                        Seed = Seed(hit, i),
                        Member = Squads.Nearest(hit),
                        Suppression = hit.Profile.Suppression,
                    });
                    if (hit.Profile.Radius > 0f)
                    {
                        Splash(hit, struck, victims);
                    }
                }

                events.Clear();
            }

            /// <summary>Same frame, shooter, target and place in the queue roll the same member, so a replay hits alike.</summary>
            private uint Seed(in DamageEvent hit, int index) =>
                math.hash(new uint4(Frame, (uint)hit.Source.Index, (uint)hit.Target.Index, (uint)index));

            /// <summary>Splash reaches squad members standing beyond the squad's radius, so the query widens by the greatest spread.</summary>
            private void Splash(in DamageEvent hit, int struck, NativeList<SpatialEntry> victims)
            {
                victims.Clear();
                var collector = new SplashCollector { Victims = victims };
                Index.Query(hit.Position, hit.Profile.Radius + math.max(Squad.MaxSpread, Squad.WorkSpread), ref collector);

                foreach (var victim in victims)
                {
                    if (!SplashReaches(hit, victim))
                    {
                        continue;
                    }

                    if (Squads.IsSquad(victim.Entity))
                    {
                        SplashMembers(hit, victim, victim.Entity == hit.Target ? struck : -1);
                        continue;
                    }

                    var gap = EntityRadius.EdgeDistance(victim.Position, victim.Radius, hit.Position, 0f);
                    if (victim.Entity != hit.Target && gap <= hit.Profile.Radius)
                    {
                        Apply(victim.Entity, hit, new Blow
                        {
                            Amount = hit.Profile.Amount * hit.Profile.SplashFactor(gap),
                            From = hit.Position,
                            Member = -1,
                        });
                    }
                }
            }

            /// <summary>Splash stays on the layers the hit reaches, so a ground burst spares aircraft above. Heals reach allies only.</summary>
            private bool SplashReaches(in DamageEvent hit, in SpatialEntry victim)
            {
                if (!Weapon.CanHit(hit.Layers, victim.Layer))
                {
                    return false;
                }

                if (hit.Profile.Amount < 0f)
                {
                    return Relations.IsAllied(hit.SourceFaction, victim.Faction);
                }

                return hit.Profile.FriendlyFire || Relations.IsHostile(hit.SourceFaction, victim.Faction)
                    || Destructibles.HasComponent(victim.Entity);
            }

            /// <summary>
            /// Scales the blow by the target's armor, routes it to sections, squad members or health, and records it.
            /// Returns the squad member struck, else -1.
            /// </summary>
            private int Apply(Entity target, in DamageEvent hit, in Blow blow)
            {
                if (!Health.TryGetComponent(target, out var health) || health.Current <= 0f)
                {
                    return -1;
                }

                if (blow.Amount < 0f)
                {
                    Heal(target, -blow.Amount, health);
                    return -1;
                }

                PassToPassengers(target, blow.Amount, health, hit, blow.From);
                var scale = Factors.Scale(target, hit);
                var struck = -1;
                var held = health.Current;
                float dealt;
                if (Squads.IsSquad(target))
                {
                    if (!Squads.Hurt(target, blow.Amount * scale, hit, blow, out var wound))
                    {
                        return -1;
                    }

                    struck = wound.Member;
                    held = wound.Held;
                    dealt = wound.Dealt;
                    health.Current = wound.SquadHealth;
                }
                else
                {
                    dealt = Buildings.IsSectioned(target)
                        ? Buildings.Route(target, Transforms[target], hit, scale)
                        : blow.Amount * scale;
                    health.Current = Buildings.Resolved(target, math.max(0f, health.Current - dealt));
                }

                Record(target, health, dealt - held, dealt, hit, blow.From);
                return struck;
            }

            /// <summary>Stores the health and reports the damage, a lethal hit's heading and who dealt it.</summary>
            private void Record(Entity target, Health health, float overkill, float dealt, in DamageEvent hit, float3 from)
            {
                Health[target] = health;
                Report(target, dealt);
                if (health.Current <= 0f && LethalHits.HasComponent(target))
                {
                    var heading = ToppleMath.Quantize(from, Transforms[target].Position);
                    LethalHits[target] = new LethalHit { Type = hit.Profile.Type, Overkill = overkill, Heading = heading };
                }

                if (Attackers.HasComponent(target) && hit.SourceFaction != 0)
                {
                    Attackers[target] = new LastAttacker { Source = hit.Source, Faction = hit.SourceFaction };
                }
            }

            private void Heal(Entity target, float amount, Health health)
            {
                if (Squads.IsSquad(target))
                {
                    health.Current = Squads.Heal(target, amount);
                }
                else
                {
                    var restored = Buildings.Repair(target, amount);
                    health.Current = Buildings.Resolved(target, math.min(health.Max, health.Current + restored));
                }

                Health[target] = health;
            }

            /// <summary>Armor that soaks the whole hit raises no event.</summary>
            private void Report(Entity target, float amount)
            {
                if (amount <= 0f || !Factions.TryGetComponent(target, out var owner))
                {
                    return;
                }

                EntityDamaged.Raise(ref Damaged, target, owner.Value);
            }

            /// <summary>Passes the share of a hit on a shelter that its walls, at their health before the hit, don't stop.</summary>
            private void PassToPassengers(Entity target, float amount, in Health health, in DamageEvent hit, float3 from)
            {
                if (hit.Profile.GarrisonFactor <= 0f || !Shelters.TryGetComponent(target, out var shelter))
                {
                    return;
                }

                var passing = hit;
                passing.Target = Entity.Null;
                passing.Origin = from;
                passing.Profile.Amount = shelter.Passing(hit, amount, Buildings.Intact(target, health));
                passing.Profile.Radius = 0f;
                if (passing.Profile.Amount > 0f)
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
