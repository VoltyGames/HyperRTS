using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Status
{
    /// <summary>
    /// Applies queued <see cref="StatusRequest"/>s, expires effects and deals their damage over time. An effect's
    /// bonuses use its type id as their source, so stacking rewrites them and expiry removes them. Runs before
    /// <c>StatSystem</c>, which is last in the group, so stat changes land the same frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct StatusEffectSystem : ISystem
    {
        private ComponentLookup<StatusEffectDefinition> _definitions;
        private ComponentLookup<EntityInfo> _infos;
        private BufferLookup<StatusStatBonus> _statBonuses;
        private BufferLookup<StatusDamageBonus> _damageBonuses;
        private BufferLookup<ActiveStatus> _statuses;
        private BufferLookup<StatModifier> _modifiers;
        private BufferLookup<DamageTakenModifier> _typedModifiers;
        private DamageWriter _damage;

        public void OnCreate(ref SystemState state)
        {
            _definitions = state.GetComponentLookup<StatusEffectDefinition>(true);
            _infos = state.GetComponentLookup<EntityInfo>(true);
            _statBonuses = state.GetBufferLookup<StatusStatBonus>(true);
            _damageBonuses = state.GetBufferLookup<StatusDamageBonus>(true);
            _statuses = state.GetBufferLookup<ActiveStatus>();
            _modifiers = state.GetBufferLookup<StatModifier>();
            _typedModifiers = state.GetBufferLookup<DamageTakenModifier>();
            _damage = new DamageWriter(ref state);

            var queue = state.EntityManager.CreateEntity(typeof(StatusQueue), typeof(StatusRequest));
            state.EntityManager.SetName(queue, "StatusQueue");
            state.RequireForUpdate<DamageQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _definitions.Update(ref state);
            _infos.Update(ref state);
            _statBonuses.Update(ref state);
            _damageBonuses.Update(ref state);
            _statuses.Update(ref state);
            _modifiers.Update(ref state);
            _typedModifiers.Update(ref state);
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());

            var now = (float)SystemAPI.Time.ElapsedTime;
            new ApplyJob
            {
                Now = now,
                Definitions = _definitions,
                Infos = _infos,
                StatBonuses = _statBonuses,
                DamageBonuses = _damageBonuses,
                Statuses = _statuses,
                Modifiers = _modifiers,
                TypedModifiers = _typedModifiers,
            }.Schedule();

            new TickJob
            {
                Now = now,
                DeltaTime = SystemAPI.Time.DeltaTime,
                Definitions = _definitions,
                Modifiers = _modifiers,
                TypedModifiers = _typedModifiers,
                Damage = _damage,
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(StatusQueue))]
        private partial struct ApplyJob : IJobEntity
        {
            public float Now;
            [ReadOnly] public ComponentLookup<StatusEffectDefinition> Definitions;
            [ReadOnly] public ComponentLookup<EntityInfo> Infos;
            [ReadOnly] public BufferLookup<StatusStatBonus> StatBonuses;
            [ReadOnly] public BufferLookup<StatusDamageBonus> DamageBonuses;
            public BufferLookup<ActiveStatus> Statuses;
            public BufferLookup<StatModifier> Modifiers;
            public BufferLookup<DamageTakenModifier> TypedModifiers;

            private void Execute(DynamicBuffer<StatusRequest> requests)
            {
                foreach (var request in requests)
                {
                    Apply(request);
                }

                requests.Clear();
            }

            private void Apply(in StatusRequest request)
            {
                if (!Statuses.TryGetBuffer(request.Target, out var statuses))
                {
                    return;
                }

                var definition = Definitions[request.Definition];
                var typeId = Infos[request.Definition].TypeId;
                var index = IndexOf(statuses, typeId);
                if (index < 0)
                {
                    statuses.Add(new ActiveStatus { TypeId = typeId });
                    index = statuses.Length - 1;
                }

                ref var status = ref statuses.ElementAt(index);
                status.EndsAt = Now + definition.Duration;
                status.Definition = request.Definition;
                status.Source = request.Source;
                status.SourceFaction = request.SourceFaction;
                var stacks = definition.NextStacks(status.Stacks);
                if (stacks != status.Stacks)
                {
                    status.Stacks = stacks;
                    Grant(request.Target, request.Definition, StatSource.Status(typeId), stacks);
                }
            }

            /// <summary>Replaces the effect's bonuses on <paramref name="target"/> with one copy per stack.</summary>
            private void Grant(Entity target, Entity definition, StatSource source, int stacks)
            {
                if (Modifiers.TryGetBuffer(target, out var modifiers))
                {
                    StatMath.RemoveSource(modifiers, source);
                    foreach (var bonus in StatBonuses[definition])
                    {
                        var modifier = bonus.Modifier;
                        modifier.Add *= stacks;
                        modifier.Percent *= stacks;
                        modifiers.Add(modifier);
                    }
                }

                if (TypedModifiers.TryGetBuffer(target, out var typed))
                {
                    DamageTakenModifier.RemoveSource(typed, source);
                    foreach (var bonus in DamageBonuses[definition])
                    {
                        var modifier = bonus.Modifier;
                        modifier.Percent *= stacks;
                        typed.Add(modifier);
                    }
                }
            }

            private static int IndexOf(DynamicBuffer<ActiveStatus> statuses, int typeId)
            {
                for (var i = 0; i < statuses.Length; i++)
                {
                    if (statuses[i].TypeId == typeId)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        /// <summary>Single-threaded: damage over time appends to the shared damage queue.</summary>
        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct TickJob : IJobEntity
        {
            public float Now;
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<StatusEffectDefinition> Definitions;
            public BufferLookup<StatModifier> Modifiers;
            public BufferLookup<DamageTakenModifier> TypedModifiers;
            public DamageWriter Damage;

            private void Execute(Entity entity, DynamicBuffer<ActiveStatus> statuses, in LocalTransform transform)
            {
                for (var i = statuses.Length - 1; i >= 0; i--)
                {
                    var status = statuses[i];
                    if (Now >= status.EndsAt)
                    {
                        Expire(entity, StatSource.Status(status.TypeId));
                        statuses.RemoveAt(i);
                        continue;
                    }

                    var definition = Definitions[status.Definition];
                    if (definition.DamagePerSecond > 0f)
                    {
                        // Damage over time has no direction, so it comes from the target itself.
                        Damage.Add(new DamageEvent
                        {
                            Target = entity,
                            Position = transform.Position,
                            Origin = transform.Position,
                            Source = status.Source,
                            SourceFaction = status.SourceFaction,
                            Amount = definition.DamagePerSecond * status.Stacks * DeltaTime,
                            Type = definition.DamageType,
                            EdgeFactor = 1f,
                        });
                    }
                }
            }

            private void Expire(Entity entity, StatSource source)
            {
                if (Modifiers.TryGetBuffer(entity, out var modifiers))
                {
                    StatMath.RemoveSource(modifiers, source);
                }

                if (TypedModifiers.TryGetBuffer(entity, out var typed))
                {
                    DamageTakenModifier.RemoveSource(typed, source);
                }
            }
        }
    }
}
