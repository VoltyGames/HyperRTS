using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Spatial;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace HyperRTS.Simulation.Fields
{
    /// <summary>
    /// Every <see cref="Interval"/>, finds who stands in each active field (finished, powered, alive), rewrites their
    /// <see cref="FieldPresence"/> and field bonuses when that changes, and queues the fields' healing and damage.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(DamageSystem))]
    public partial struct AreaFieldSystem : ISystem
    {
        public const float Interval = 0.25f;

        private NativeParallelMultiHashMap<Entity, FieldPresence> _hits;
        private ComponentLookup<AreaField> _fields;
        private BufferLookup<AreaFieldBonus> _bonuses;
        private BufferLookup<StatModifier> _modifiers;
        private ComponentLookup<LocalTransform> _transforms;
        private DamageWriter _damage;
        private EntityQuery _fieldQuery;
        private bool _hadFields;
        private float _elapsed;

        public void OnCreate(ref SystemState state)
        {
            _hits = new NativeParallelMultiHashMap<Entity, FieldPresence>(256, Allocator.Persistent);
            _fields = state.GetComponentLookup<AreaField>(true);
            _bonuses = state.GetBufferLookup<AreaFieldBonus>(true);
            _modifiers = state.GetBufferLookup<StatModifier>();
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _damage = new DamageWriter(ref state);
            _fieldQuery = SystemAPI.QueryBuilder().WithAll<AreaField>().Build();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<DamageQueue>();
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            _hits.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _elapsed += SystemAPI.Time.DeltaTime;
            if (_elapsed < Interval)
            {
                return;
            }

            _elapsed -= Interval;

            // Without fields there is nothing to apply; one more pass after the last one goes clears its bonuses.
            var hasFields = !_fieldQuery.IsEmptyIgnoreFilter;
            if (!hasFields && !_hadFields)
            {
                return;
            }

            _hadFields = hasFields;
            _fields.Update(ref state);
            _bonuses.Update(ref state);
            _modifiers.Update(ref state);
            _transforms.Update(ref state);
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());

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
                Fields = _fields,
                Bonuses = _bonuses,
                Modifiers = _modifiers,
                Transforms = _transforms,
                Damage = _damage,
            }.Schedule();
        }

        [BurstCompile]
        private struct ClearJob : IJob
        {
            public NativeParallelMultiHashMap<Entity, FieldPresence> Hits;

            public void Execute() => Hits.Clear();
        }

        [BurstCompile]
        [WithNone(typeof(ConstructionProgress), typeof(Unpowered), typeof(Dead))]
        private partial struct CollectJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public FactionRelations Relations;
            public NativeParallelMultiHashMap<Entity, FieldPresence> Hits;

            private void Execute(Entity entity, in AreaField field, in LocalTransform transform, in Faction faction)
            {
                var collector = new FieldCollector
                {
                    Relations = Relations,
                    Affects = field.Affects,
                    Hits = Hits,
                    Presence = new FieldPresence { FieldId = field.FieldId, Source = entity, SourceFaction = faction.Value },
                };
                Index.Query(transform.Position, field.Radius, ref collector);
            }
        }

        private struct FieldCollector : ISpatialVisitor
        {
            public FactionRelations Relations;
            public FieldTargets Affects;
            public NativeParallelMultiHashMap<Entity, FieldPresence> Hits;
            public FieldPresence Presence;

            public void Visit(in SpatialEntry entry)
            {
                if (FieldRules.Affects(Affects, Presence.SourceFaction, entry, Relations))
                {
                    Hits.Add(entry.Entity, Presence);
                }
            }
        }

        /// <summary>Single-threaded: field damage goes to the one damage queue in a stable order.</summary>
        [BurstCompile]
        private partial struct ApplyJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, FieldPresence> Hits;
            [ReadOnly] public ComponentLookup<AreaField> Fields;
            [ReadOnly] public BufferLookup<AreaFieldBonus> Bonuses;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public BufferLookup<StatModifier> Modifiers;
            public DamageWriter Damage;

            private void Execute(Entity entity, DynamicBuffer<FieldPresence> presence)
            {
                var current = FieldRules.Resolve(Hits, entity);
                if (!FieldRules.SameFields(presence, current))
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

            private void SwapBonuses(Entity entity, DynamicBuffer<FieldPresence> previous,
                in FixedList512Bytes<FieldPresence> current)
            {
                if (!Modifiers.TryGetBuffer(entity, out var modifiers))
                {
                    return;
                }

                foreach (var item in previous)
                {
                    StatMath.RemoveSource(modifiers, item.FieldId);
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

            private void Tick(Entity entity, in FieldPresence item)
            {
                var field = Fields[item.Source];
                var hit = new DamageEvent
                {
                    Target = entity,
                    Origin = Transforms[item.Source].Position,
                    Source = item.Source,
                    SourceFaction = item.SourceFaction,
                    Type = field.DamageType,
                };

                if (field.HealPerSecond > 0f)
                {
                    hit.Amount = -field.HealPerSecond * Interval;
                    Damage.Add(hit);
                }

                if (field.DamagePerSecond > 0f)
                {
                    hit.Amount = field.DamagePerSecond * Interval;
                    Damage.Add(hit);
                }
            }
        }
    }
}
