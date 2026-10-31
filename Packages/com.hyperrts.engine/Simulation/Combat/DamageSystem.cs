using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Resolves the frame's <see cref="DamageEvent"/>s in order. Heals skip every multiplier.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderLast = true)]
    public partial struct DamageSystem : ISystem
    {
        private ComponentLookup<Health> _health;
        private ComponentLookup<LastAttacker> _attackers;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<ArmorFacing> _facing;
        private BufferLookup<ArmorModifier> _armor;
        private BufferLookup<StatModifier> _modifiers;
        private BufferLookup<DamageTakenModifier> _typedModifiers;
        private ComponentLookup<Destructible> _destructibles;
        private ComponentLookup<ToppleHeading> _toppleHeadings;
        private ComponentLookup<Faction> _factions;
        private BufferLookup<EntityDamaged> _damaged;
        private ComponentLookup<Shelter> _shelters;
        private BufferLookup<ShelterHit> _shelterHits;
        private ComponentLookup<Sections> _buildings;
        private BufferLookup<Section> _sections;
        private BufferLookup<SectionFactor> _sectionFactors;

        public void OnCreate(ref SystemState state)
        {
            _health = state.GetComponentLookup<Health>();
            _attackers = state.GetComponentLookup<LastAttacker>();
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _facing = state.GetComponentLookup<ArmorFacing>(true);
            _armor = state.GetBufferLookup<ArmorModifier>(true);
            _modifiers = state.GetBufferLookup<StatModifier>(true);
            _typedModifiers = state.GetBufferLookup<DamageTakenModifier>(true);
            _destructibles = state.GetComponentLookup<Destructible>(true);
            _toppleHeadings = state.GetComponentLookup<ToppleHeading>();
            _factions = state.GetComponentLookup<Faction>(true);
            _damaged = state.GetBufferLookup<EntityDamaged>();
            _shelters = state.GetComponentLookup<Shelter>(true);
            _shelterHits = state.GetBufferLookup<ShelterHit>();
            _buildings = state.GetComponentLookup<Sections>();
            _sections = state.GetBufferLookup<Section>();
            _sectionFactors = state.GetBufferLookup<SectionFactor>(true);

            var queue = state.EntityManager.CreateEntity(typeof(DamageQueue), typeof(DamageEvent));
            state.EntityManager.SetName(queue, "DamageQueue");
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<GameEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _health.Update(ref state);
            _attackers.Update(ref state);
            _transforms.Update(ref state);
            _facing.Update(ref state);
            _armor.Update(ref state);
            _modifiers.Update(ref state);
            _typedModifiers.Update(ref state);
            _destructibles.Update(ref state);
            _toppleHeadings.Update(ref state);
            _factions.Update(ref state);
            _damaged.Update(ref state);
            _shelters.Update(ref state);
            _shelterHits.Update(ref state);
            _buildings.Update(ref state);
            _sections.Update(ref state);
            _sectionFactors.Update(ref state);

            new ResolveJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Health = _health,
                Attackers = _attackers,
                Transforms = _transforms,
                Facing = _facing,
                Armor = _armor,
                Modifiers = _modifiers,
                TypedModifiers = _typedModifiers,
                Destructibles = _destructibles,
                FallSources = _toppleHeadings,
                Factions = _factions,
                Damaged = _damaged,
                Shelters = _shelters,
                ShelterHits = _shelterHits,
                Buildings = _buildings,
                BuildingSections = _sections,
                SectionFactors = _sectionFactors,
                Events = SystemAPI.GetSingletonEntity<GameEvents>(),
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
            [ReadOnly] public ComponentLookup<ArmorFacing> Facing;
            [ReadOnly] public BufferLookup<ArmorModifier> Armor;
            [ReadOnly] public BufferLookup<StatModifier> Modifiers;
            [ReadOnly] public BufferLookup<DamageTakenModifier> TypedModifiers;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            public ComponentLookup<ToppleHeading> FallSources;
            [ReadOnly] public ComponentLookup<Faction> Factions;
            public BufferLookup<EntityDamaged> Damaged;
            [ReadOnly] public ComponentLookup<Shelter> Shelters;
            public BufferLookup<ShelterHit> ShelterHits;
            public ComponentLookup<Sections> Buildings;
            public BufferLookup<Section> BuildingSections;
            [ReadOnly] public BufferLookup<SectionFactor> SectionFactors;
            public Entity Events;

            private void Execute(DynamicBuffer<DamageEvent> events)
            {
                var victims = new NativeList<SpatialEntry>(16, Allocator.Temp);
                var damaged = Damaged[Events];
                foreach (var hit in events)
                {
                    Apply(hit.Target, hit.Amount, hit, hit.Origin, damaged);
                    if (hit.Radius > 0f)
                    {
                        Splash(hit, victims, damaged);
                    }
                }

                events.Clear();
            }

            private void Splash(in DamageEvent hit, NativeList<SpatialEntry> victims,
                DynamicBuffer<EntityDamaged> damaged)
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
                    var amount = hit.Amount * CombatMath.SplashFactor(gap, hit.Radius, hit.EdgeFactor);
                    Apply(victim.Entity, amount, hit, hit.Position, damaged);
                }
            }

            /// <summary>Splash stays on the layers the hit reaches, so a ground burst spares aircraft above. Heals reach allies only.</summary>
            private bool SplashReaches(in DamageEvent hit, in SpatialEntry victim)
            {
                if (!CombatMath.CanHit(hit.Reach, victim.Layer))
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
            private void Apply(Entity target, float amount, in DamageEvent hit, float3 from,
                DynamicBuffer<EntityDamaged> damaged)
            {
                if (!Health.TryGetComponent(target, out var health) || health.Current <= 0f)
                {
                    return;
                }

                if (amount < 0f)
                {
                    health.Current = math.min(health.Max, health.Current + Repair(target, -amount));
                    Health[target] = health;
                    return;
                }

                PassToPassengers(target, amount, health, hit, from);
                var scale = CombatMath.ArmorMultiplier(Armor, target, hit.Type) * FacingMultiplier(target, hit.Origin) *
                            StatMath.DamageTaken(Modifiers, target) * TypedMultiplier(target, hit.Type);
                amount = BuildingSections.HasBuffer(target) ? Route(target, hit, from, scale) : amount * scale;
                health.Current = math.max(0f, health.Current - amount);
                Health[target] = health;
                Report(target, amount, damaged);
                if (health.Current <= 0f && FallSources.HasComponent(target))
                {
                    var fall = ToppleMath.Quantize(from, Transforms[target].Position);
                    FallSources[target] = new ToppleHeading { Heading = fall };
                }

                if (Attackers.HasComponent(target) && hit.SourceFaction != 0)
                {
                    Attackers[target] = new LastAttacker { Source = hit.Source, Faction = hit.SourceFaction };
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
                var intact = BuildingSections.TryGetBuffer(target, out var sections)
                    ? Sections.Intact(sections)
                    : health.Current / math.max(health.Max, 1e-4f);
                passing.Amount = shelter.Passing(hit, amount, intact);
                passing.Radius = 0f;
                if (passing.Amount > 0f)
                {
                    ShelterHits[target].Add(new ShelterHit { Hit = passing });
                }
            }

            /// <summary>The hit on a sectioned building's sections, in its space; returns the damage dealt.</summary>
            private float Route(Entity target, in DamageEvent hit, float3 from, float scale)
            {
                var transform = Transforms[target];
                var building = Buildings[target];
                var dealt = building.Route(BuildingSections[target], SectionFactors[target], hit,
                    transform.InverseTransformPoint(hit.Origin), transform.InverseTransformPoint(hit.Position), scale,
                    target == hit.Target);
                building.Side = ToppleMath.Quantize(from, transform.Position);
                Buildings[target] = building;
                return dealt;
            }

            /// <summary>Health restored by a heal of <paramref name="amount"/>, rebuilding broken sections first.</summary>
            private float Repair(Entity target, float amount)
            {
                if (!BuildingSections.TryGetBuffer(target, out var sections))
                {
                    return amount;
                }

                var building = Buildings[target];
                var restored = building.Repair(sections, amount);
                Buildings[target] = building;
                return restored;
            }

            /// <summary>Armor that soaks the whole hit raises no event.</summary>
            private void Report(Entity target, float amount, DynamicBuffer<EntityDamaged> damaged)
            {
                if (amount <= 0f || !Factions.TryGetComponent(target, out var owner))
                {
                    return;
                }

                if (owner.Value != Faction.Neutral)
                {
                    damaged.Add(new EntityDamaged { Entity = target, Faction = owner.Value });
                }
            }

            private float TypedMultiplier(Entity target, UnityObjectRef<DamageType> type) =>
                TypedModifiers.TryGetBuffer(target, out var modifiers)
                    ? DamageTakenModifier.Multiplier(modifiers, type)
                    : 1f;

            private float FacingMultiplier(Entity target, float3 origin) =>
                Facing.TryGetComponent(target, out var facing) && Transforms.TryGetComponent(target, out var transform)
                    ? facing.Multiplier(transform, origin)
                    : 1f;
        }

        private struct SplashCollector : ISpatialVisitor
        {
            public NativeList<SpatialEntry> Victims;

            public void Visit(in SpatialEntry entry) => Victims.Add(entry);
        }
    }
}
