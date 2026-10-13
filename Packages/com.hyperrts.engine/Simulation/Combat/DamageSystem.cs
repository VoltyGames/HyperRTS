using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Spatial;
using HyperRTS.Simulation.Stats;
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
        private ComponentLookup<Destructible> _destructibles;
        private ComponentLookup<FellFrom> _fellFrom;

        public void OnCreate(ref SystemState state)
        {
            _health = state.GetComponentLookup<Health>();
            _attackers = state.GetComponentLookup<LastAttacker>();
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _facing = state.GetComponentLookup<ArmorFacing>(true);
            _armor = state.GetBufferLookup<ArmorModifier>(true);
            _modifiers = state.GetBufferLookup<StatModifier>(true);
            _destructibles = state.GetComponentLookup<Destructible>(true);
            _fellFrom = state.GetComponentLookup<FellFrom>();

            var queue = state.EntityManager.CreateEntity(typeof(DamageQueue), typeof(DamageEvent));
            state.EntityManager.SetName(queue, "DamageQueue");
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
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
            _destructibles.Update(ref state);
            _fellFrom.Update(ref state);

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
                Destructibles = _destructibles,
                FallSources = _fellFrom,
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
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            public ComponentLookup<FellFrom> FallSources;

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
                    var amount = hit.Amount * CombatMath.SplashFactor(gap, hit.Radius, hit.EdgeFactor);
                    Apply(victim.Entity, amount, hit, hit.Position);
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
            private void Apply(Entity target, float amount, in DamageEvent hit, float3 from)
            {
                if (!Health.TryGetComponent(target, out var health) || health.Current <= 0f)
                {
                    return;
                }

                if (amount < 0f)
                {
                    health.Current = math.min(health.Max, health.Current - amount);
                    Health[target] = health;
                    return;
                }

                amount *= CombatMath.ArmorMultiplier(Armor, target, hit.Type) * FacingMultiplier(target, hit.Origin) *
                          StatMath.DamageTaken(Modifiers, target);
                health.Current = math.max(0f, health.Current - amount);
                Health[target] = health;
                if (health.Current <= 0f && FallSources.HasComponent(target))
                {
                    FallSources[target] = new FellFrom { Position = from };
                }

                if (Attackers.HasComponent(target) && hit.SourceFaction != 0)
                {
                    Attackers[target] = new LastAttacker { Source = hit.Source, Faction = hit.SourceFaction };
                }
            }

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
