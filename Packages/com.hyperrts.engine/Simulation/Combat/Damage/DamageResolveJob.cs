using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>Resolves each queued <see cref="DamageEvent"/> onto its victims in order.</summary>
    [BurstCompile]
    [WithAll(typeof(DamageQueue))]
    internal partial struct DamageResolveJob : IJobEntity
    {
        [ReadOnly] public SpatialIndex Index;
        public OwnerRelations Relations;
        public ComponentLookup<Health> Health;
        public ComponentLookup<LastAttacker> Attackers;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        public DamageFactors Factors;
        [ReadOnly] public ComponentLookup<Destructible> Destructibles;
        public ComponentLookup<LethalHit> LethalHits;
        [ReadOnly] public ComponentLookup<Owner> Owners;
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
                return Relations.IsAllied(hit.SourceOwner, victim.Owner);
            }

            return hit.Profile.FriendlyFire || Relations.IsHostile(hit.SourceOwner, victim.Owner)
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
                dealt = Dealt(target, hit, blow.Amount, scale);
                health.Current = Buildings.Resolved(target, math.max(0f, health.Current - dealt));
            }

            Record(target, health, dealt - held, dealt, hit, blow.From);
            return struck;
        }

        private float Dealt(Entity target, in DamageEvent hit, float amount, float scale) =>
            Buildings.IsSectioned(target) ? Buildings.Route(target, Transforms[target], hit, scale) : amount * scale;

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

            if (Attackers.HasComponent(target) && hit.SourceOwner != 0)
            {
                Attackers[target] = new LastAttacker { Source = hit.Source, Owner = hit.SourceOwner };
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
            if (amount <= 0f || !Owners.TryGetComponent(target, out var owner))
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

        /// <summary>Each living member but the one a direct hit already struck takes the blast by its own distance.</summary>
        private void SplashMembers(in DamageEvent hit, in SpatialEntry squad, int struck)
        {
            var members = Squads.Members(squad.Entity);
            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                var gap = EntityRadius.EdgeDistance(member.Position(squad.Position), SquadMember.Radius, hit.Position, 0f);
                if (i != struck && member.Alive && gap <= hit.Profile.Radius)
                {
                    var factor = hit.Profile.SplashFactor(gap);
                    Apply(squad.Entity, hit, new Blow
                    {
                        Amount = hit.Profile.Amount * factor,
                        From = hit.Position,
                        Member = i,
                        Suppression = hit.Profile.Suppression * factor / members.Length,
                    });
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
