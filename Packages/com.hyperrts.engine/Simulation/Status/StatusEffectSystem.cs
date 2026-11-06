using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Status
{
    /// <summary>
    /// Applies queued <see cref="StatusRequest"/>s, expires effects and deals their damage over time, before <see
    /// cref="DamageSystem"/>. An effect's bonuses use its type id as their source.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(DamageSystem))]
    public partial struct StatusEffectSystem : ISystem
    {
        private EventWriter<DamageEvent> _damage;

        public void OnCreate(ref SystemState state)
        {
            _damage = new EventWriter<DamageEvent>(ref state);

            EventQueues.Create<StatusQueue>(ref state, typeof(StatusRequest));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damage.Update(ref state);

            var now = (float)SystemAPI.Time.ElapsedTime;
            new ApplyJob
            {
                Now = now,
                Definitions = SystemAPI.GetComponentLookup<StatusEffectDefinition>(true),
                Infos = SystemAPI.GetComponentLookup<EntityInfo>(true),
                Bonuses = SystemAPI.GetBufferLookup<GrantedModifier>(true),
                Statuses = SystemAPI.GetBufferLookup<ActiveStatus>(false),
                Modifiers = SystemAPI.GetBufferLookup<StatModifier>(false),
            }.Schedule();

            new TickJob
            {
                Now = now,
                DeltaTime = SystemAPI.Time.DeltaTime,
                Definitions = SystemAPI.GetComponentLookup<StatusEffectDefinition>(true),
                Modifiers = SystemAPI.GetBufferLookup<StatModifier>(false),
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
            [ReadOnly] public BufferLookup<GrantedModifier> Bonuses;
            public BufferLookup<ActiveStatus> Statuses;
            public BufferLookup<StatModifier> Modifiers;

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
                var index = FindStatus(statuses, typeId);
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
                var modifiers = Modifiers[target];
                StatMath.RemoveSource(modifiers, source);
                foreach (var bonus in Bonuses[definition])
                {
                    var modifier = bonus.Modifier;
                    modifier.Add *= stacks;
                    modifier.Percent *= stacks;
                    modifiers.Add(modifier);
                }
            }

            private static int FindStatus(DynamicBuffer<ActiveStatus> statuses, int typeId)
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
            public EventWriter<DamageEvent> Damage;

            private void Execute(Entity entity, DynamicBuffer<ActiveStatus> statuses, in LocalTransform transform)
            {
                for (var i = statuses.Length - 1; i >= 0; i--)
                {
                    var status = statuses[i];
                    if (Now >= status.EndsAt)
                    {
                        StatMath.RemoveSource(Modifiers[entity], StatSource.Status(status.TypeId));
                        statuses.RemoveAt(i);
                        continue;
                    }

                    var definition = Definitions[status.Definition];
                    if (definition.DamagePerSecond > 0f)
                    {
                        // Damage over time has no direction, so it comes from the target itself.
                        Damage.Add(DamageEvent.Direct(entity, transform.Position, status.Source, status.SourceFaction,
                            definition.DamagePerSecond * status.Stacks * DeltaTime).Typed(definition.Damage));
                    }
                }
            }
        }
    }
}
