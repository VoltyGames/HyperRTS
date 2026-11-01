using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>
    /// Every <see cref="Interval"/>, finds who stands in each active aura (finished, powered, alive), rewrites their
    /// <see cref="AuraPresence"/> and aura bonuses when that changes, and queues the auras' healing and damage.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(DamageSystem))]
    public partial struct AuraSystem : ISystem
    {
        public const float Interval = 0.25f;

        private NativeParallelMultiHashMap<Entity, AuraPresence> _hits;
        private ComponentLookup<Aura> _auras;
        private BufferLookup<AuraBonus> _bonuses;
        private BufferLookup<StatModifier> _modifiers;
        private ComponentLookup<LocalTransform> _transforms;
        private EventWriter<DamageEvent> _damage;
        private EntityQuery _auraQuery;
        private bool _hadAuras;
        private float _elapsed;

        public void OnCreate(ref SystemState state)
        {
            _hits = new NativeParallelMultiHashMap<Entity, AuraPresence>(256, Allocator.Persistent);
            _auras = state.GetComponentLookup<Aura>(true);
            _bonuses = state.GetBufferLookup<AuraBonus>(true);
            _modifiers = state.GetBufferLookup<StatModifier>();
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _damage = new EventWriter<DamageEvent>(ref state);
            _auraQuery = SystemAPI.QueryBuilder().WithAll<Aura>().Build();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            _hits.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!IsDue(SystemAPI.Time.DeltaTime))
            {
                return;
            }

            _auras.Update(ref state);
            _bonuses.Update(ref state);
            _modifiers.Update(ref state);
            _transforms.Update(ref state);
            _damage.Update(ref state);

            state.Dependency = new ClearJob { Hits = _hits }.Schedule(state.Dependency);
            new CollectJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Hits = _hits,
            }.Schedule();
            new ApplyJob
            {
                Hits = _hits,
                Auras = _auras,
                Bonuses = _bonuses,
                Modifiers = _modifiers,
                Transforms = _transforms,
                Damage = _damage,
            }.Schedule();
        }

        /// <summary>
        /// Every <see cref="Interval"/> while auras exist. Without auras there is nothing to apply, but one more pass
        /// after the last one goes clears its bonuses.
        /// </summary>
        private bool IsDue(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed < Interval)
            {
                return false;
            }

            _elapsed -= Interval;
            var hasAuras = !_auraQuery.IsEmptyIgnoreFilter;
            var due = hasAuras || _hadAuras;
            _hadAuras = hasAuras;
            return due;
        }

        [BurstCompile]
        private struct ClearJob : IJob
        {
            public NativeParallelMultiHashMap<Entity, AuraPresence> Hits;

            public void Execute() => Hits.Clear();
        }

        [BurstCompile]
        [WithNone(typeof(Offline), typeof(Dead))]
        private partial struct CollectJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public FactionRelations Relations;
            public NativeParallelMultiHashMap<Entity, AuraPresence> Hits;

            private void Execute(Entity entity, in Aura aura, in LocalTransform transform, in Faction faction)
            {
                var collector = new AuraCollector
                {
                    Relations = Relations,
                    Affects = aura.Affects,
                    Hits = Hits,
                    Presence = new AuraPresence { AuraId = aura.AuraId, Source = entity, SourceFaction = faction.Value },
                };
                Index.Query(transform.Position, aura.Radius, ref collector);
            }
        }

        private struct AuraCollector : ISpatialVisitor
        {
            public FactionRelations Relations;
            public AuraTargets Affects;
            public NativeParallelMultiHashMap<Entity, AuraPresence> Hits;
            public AuraPresence Presence;

            public void Visit(in SpatialEntry entry)
            {
                if (AuraRules.Affects(Affects, Presence.SourceFaction, entry, Relations))
                {
                    Hits.Add(entry.Entity, Presence);
                }
            }
        }

        /// <summary>Single-threaded: aura damage goes to the one damage queue in a stable order.</summary>
        [BurstCompile]
        private partial struct ApplyJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, AuraPresence> Hits;
            [ReadOnly] public ComponentLookup<Aura> Auras;
            [ReadOnly] public BufferLookup<AuraBonus> Bonuses;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public BufferLookup<StatModifier> Modifiers;
            public EventWriter<DamageEvent> Damage;

            private void Execute(Entity entity, DynamicBuffer<AuraPresence> presence)
            {
                var current = AuraRules.Resolve(Hits, entity);
                if (!AuraRules.SameAuras(presence, current))
                {
                    SwapBonuses(entity, presence, current);
                    presence.Clear();
                    foreach (var item in current)
                    {
                        presence.Add(item);
                    }
                }

                foreach (var item in current)
                {
                    Tick(entity, item);
                }
            }

            private void SwapBonuses(Entity entity, DynamicBuffer<AuraPresence> previous,
                in FixedList512Bytes<AuraPresence> current)
            {
                if (!Modifiers.TryGetBuffer(entity, out var modifiers))
                {
                    return;
                }

                foreach (var item in previous)
                {
                    StatMath.RemoveSource(modifiers, StatSource.Aura(item.AuraId));
                }

                foreach (var item in current)
                {
                    if (Bonuses.TryGetBuffer(item.Source, out var bonuses))
                    {
                        foreach (var bonus in bonuses)
                        {
                            modifiers.Add(bonus.Modifier);
                        }
                    }
                }
            }

            private void Tick(Entity entity, in AuraPresence item)
            {
                var aura = Auras[item.Source];
                var hit = new DamageEvent
                {
                    Target = entity,
                    Origin = Transforms[item.Source].Position,
                    Source = item.Source,
                    SourceFaction = item.SourceFaction,
                    Type = aura.DamageType,
                };

                if (aura.HealPerSecond > 0f)
                {
                    hit.Amount = -aura.HealPerSecond * Interval;
                    Damage.Add(hit);
                }

                if (aura.DamagePerSecond > 0f)
                {
                    hit.Amount = aura.DamagePerSecond * Interval;
                    Damage.Add(hit);
                }
            }
        }
    }
}
