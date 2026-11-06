using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Infantry;
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
    /// Resolves the frame's <see cref="DamageEvent"/>s in order. Heals skip every multiplier. A direct hit on a squad
    /// lands on one living <see cref="SquadMember"/> (a blast's on the nearest), rolled by seed and favouring the
    /// uncovered; splash hurts each by its distance from the blast; cover cuts hits from its front.
    /// </summary>
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
            state.RequireForUpdate<SimulationFrame>();
            state.RequireForUpdate<CoverRules>();
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
                Members = SystemAPI.GetBufferLookup<SquadMember>(false),
                Suppressions = SystemAPI.GetComponentLookup<Suppression>(false),
                Frame = SystemAPI.GetSingleton<SimulationFrame>().Value,
                Cover = SystemAPI.GetSingleton<CoverRules>(),
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
            public BufferLookup<SquadMember> Members;
            public ComponentLookup<Suppression> Suppressions;
            public uint Frame;
            public CoverRules Cover;

            private void Execute(DynamicBuffer<DamageEvent> events)
            {
                var victims = new NativeList<SpatialEntry>(16, Allocator.Temp);
                for (var i = 0; i < events.Length; i++)
                {
                    var hit = events[i];
                    var struck = Apply(hit.Target, hit.Amount, hit, hit.Origin, Seed(hit, i), Nearest(hit), hit.Suppression);
                    if (hit.Radius > 0f)
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
                Index.Query(hit.Position, hit.Radius + SquadMember.MaxSpread, ref collector);

                foreach (var victim in victims)
                {
                    if (!SplashReaches(hit, victim))
                    {
                        continue;
                    }

                    if (Members.HasBuffer(victim.Entity))
                    {
                        SplashMembers(hit, victim, victim.Entity == hit.Target ? struck : -1);
                        continue;
                    }

                    var gap = EntityRadius.EdgeDistance(victim.Position, victim.Radius, hit.Position, 0f);
                    if (victim.Entity != hit.Target && gap <= hit.Radius)
                    {
                        Apply(victim.Entity, hit.Amount * hit.SplashFactor(gap), hit, hit.Position, 0u);
                    }
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

            /// <summary>
            /// A lethal hit fells a <see cref="Topple"/> target away from <paramref name="from"/>. A squad's hit lands on
            /// <paramref name="member"/>, or on one picked by <paramref name="seed"/> when that is -1, and adds
            /// <paramref name="suppression"/> past that member's cover; returns the member struck, else -1.
            /// </summary>
            private int Apply(Entity target, float amount, in DamageEvent hit, float3 from, uint seed, int member = -1,
                float suppression = 0f)
            {
                if (!Health.TryGetComponent(target, out var health) || health.Current <= 0f)
                {
                    return -1;
                }

                var hasMembers = Members.TryGetBuffer(target, out var members);
                if (amount < 0f)
                {
                    Heal(target, -amount, hasMembers, members, health);
                    return -1;
                }

                PassToPassengers(target, amount, health, hit, from);
                var scale = Factors.Scale(target, hit);
                amount = Buildings.IsSectioned(target)
                    ? Buildings.Route(target, Transforms[target], hit, scale)
                    : amount * scale;
                var held = health.Current;
                if (hasMembers)
                {
                    var squad = Transforms[target].Position;
                    member = member >= 0 ? member : SquadMember.PickExposed(members, squad, seed, Cover, from, hit.CoverPiercing);
                    if (member < 0)
                    {
                        return -1;
                    }

                    amount *= members[member].ExposureTo(Cover, squad, from, hit.CoverPiercing);
                    held = members[member].Health;
                    health.Current = SquadMember.Hurt(members, member, amount);
                    Suppress(target, members[member], suppression, hit, from);
                }
                else
                {
                    health.Current = Buildings.Resolved(target, math.max(0f, health.Current - amount));
                }

                var overkill = amount - held;
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

                return hasMembers ? member : -1;
            }

            private void Heal(Entity target, float amount, bool hasMembers, DynamicBuffer<SquadMember> members,
                Health health)
            {
                if (hasMembers)
                {
                    health.Current = SquadMember.Heal(members, amount);
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
