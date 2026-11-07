using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>
    /// Records the <see cref="BaseStat"/>s of newly spawned entities from their authored components, before any phase
    /// can write a live stat, so <see cref="StatSystem"/> always modifies the authored values.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct StatBaseSystem : ISystem
    {
        private StatFieldTable _table;
        private NativeArray<EntityQuery> _queries;
        private EntityQuery _pending;

        public void OnCreate(ref SystemState state)
        {
            _pending = SystemAPI.QueryBuilder().WithAll<StatBasesPending>().Build();
            _table = StatFieldTable.Find(Allocator.Persistent);
            _queries = new NativeArray<EntityQuery>(_table.Components.Length, Allocator.Persistent);
            for (var i = 0; i < _queries.Length; i++)
            {
                _queries[i] = state.GetEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly(_table.Components[i].Component.TypeIndex),
                        ComponentType.ReadWrite<BaseStat>(), ComponentType.ReadOnly<StatBasesPending>(),
                    },
                });
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            _table.Dispose();
            _queries.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_pending.IsEmpty)
            {
                return;
            }

            for (var i = 0; i < _queries.Length; i++)
            {
                var statComponent = _table.Components[i];
                state.Dependency = new FieldJob
                {
                    Component = state.GetDynamicComponentTypeHandle(
                        ComponentType.ReadOnly(statComponent.Component.TypeIndex)),
                    Floats = statComponent.Floats,
                    Fields = _table.FieldsFor(statComponent),
                    Bases = SystemAPI.GetBufferTypeHandle<BaseStat>(false),
                }.ScheduleParallel(_queries[i], state.Dependency);
            }

            new HealthJob().ScheduleParallel();
        }

        [BurstCompile]
        private struct FieldJob : IJobChunk
        {
            [ReadOnly] public DynamicComponentTypeHandle Component;
            public int Floats;
            [ReadOnly] public NativeArray<StatField> Fields;
            public BufferTypeHandle<BaseStat> Bases;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var values = chunk.GetDynamicComponentDataArrayReinterpret<float>(ref Component, Floats * sizeof(float));
                var bases = chunk.GetBufferAccessorRW(ref Bases);
                var entities = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entities.NextEntityIndex(out var i))
                {
                    foreach (var field in Fields)
                    {
                        bases[i].Add(new BaseStat { Stat = field.Stat, Value = values[i * Floats + field.Index] });
                    }
                }
            }
        }

        /// <summary>Runs after the field jobs, which visit only entities still pending.</summary>
        [BurstCompile]
        private partial struct HealthJob : IJobEntity
        {
            // Modifiers are taken writable so StatSystem's change filter applies any added before the bases existed.
            private void Execute(EnabledRefRW<StatBasesPending> pending, in Health health,
                DynamicBuffer<BaseStat> bases, DynamicBuffer<StatModifier> modifiers)
            {
                bases.Add(new BaseStat { Stat = Stat.MaxHealth, Value = health.Max });
                pending.ValueRW = false;
            }
        }
    }
}
