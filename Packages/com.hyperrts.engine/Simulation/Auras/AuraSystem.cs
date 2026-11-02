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
        private EventWriter<DamageEvent> _damage;
        private EntityQuery _auraQuery;
        private bool _hadAuras;
        private float _elapsed;

        public void OnCreate(ref SystemState state)
        {
            _hits = new NativeParallelMultiHashMap<Entity, AuraPresence>(256, Allocator.Persistent);
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
                Auras = SystemAPI.GetComponentLookup<Aura>(true),
                Bonuses = SystemAPI.GetBufferLookup<GrantedModifier>(true),
                Modifiers = SystemAPI.GetBufferLookup<StatModifier>(false),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Damage = _damage,
            }.Schedule();
        }

        /// <summary>
        /// Every <see cref="Interval"/> while auras exist. Without auras there is nothing to apply, but one more pass
        /// after the last one goes clears its bonuses.
        /// </summary>
        private bool IsDue(float deltaTime)
        {
            if (IntervalTimer.Advance(ref _elapsed, Interval, deltaTime) == 0)
            {
                return false;
            }

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
            [ReadOnly] public BufferLookup<GrantedModifier> Bonuses;
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
                var position = Transforms[entity].Position;
                var origin = Transforms[item.Source].Position;
                if (aura.HealPerSecond > 0f)
                {
                    Damage.Add(DamageEvent.OverTime(entity, position, origin, item.Source, item.SourceFaction,
                        -aura.HealPerSecond * Interval, aura.DamageType));
                }

                if (aura.DamagePerSecond > 0f)
                {
                    Damage.Add(DamageEvent.OverTime(entity, position, origin, item.Source, item.SourceFaction,
                        aura.DamagePerSecond * Interval, aura.DamageType));
                }
            }
        }
    }
}
