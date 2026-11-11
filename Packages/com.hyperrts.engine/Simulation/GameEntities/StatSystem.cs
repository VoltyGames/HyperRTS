using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>
    /// Rewrites every live stat from its recorded base and its <see cref="StatModifier"/>s whenever they change.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct StatSystem : ISystem
    {
        /// <summary>Floor on the fire-rate multiplier, keeping the cooldown finite under heavy slows.</summary>
        public const float MinFireRate = 0.01f;

        private StatFieldTable _table;
        private NativeArray<EntityQuery> _queries;

        public void OnCreate(ref SystemState state)
        {
            _table = StatFieldTable.Find(Allocator.Persistent);
            _queries = new NativeArray<EntityQuery>(_table.Components.Length, Allocator.Persistent);
            for (var i = 0; i < _queries.Length; i++)
            {
                var query = state.GetEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        _table.Components[i].Component, ComponentType.ReadOnly<StatModifier>(),
                        ComponentType.ReadOnly<BaseStat>(),
                    },
                    Disabled = new[] { ComponentType.ReadOnly<StatBasesPending>() },
                });
                query.SetChangedVersionFilter(ComponentType.ReadOnly<StatModifier>());
                _queries[i] = query;
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
            new MaxHealthJob().ScheduleParallel();
            new WeaponJob().ScheduleParallel();
            for (var i = 0; i < _queries.Length; i++)
            {
                var statComponent = _table.Components[i];
                state.Dependency = new FieldJob
                {
                    Component = state.GetDynamicComponentTypeHandle(statComponent.Component),
                    Floats = statComponent.Floats,
                    Fields = _table.FieldsFor(statComponent),
                    Modifiers = SystemAPI.GetBufferTypeHandle<StatModifier>(true),
                    Bases = SystemAPI.GetBufferTypeHandle<BaseStat>(true),
                }.ScheduleParallel(_queries[i], state.Dependency);
            }
        }

        /// <summary>
        /// The cooldown for a fire rate of <c>(1 / baseCooldown + Add) × (1 + ΣPercent)</c> shots per second, worked in
        /// cooldown space so an unmodified weapon keeps its exact authored cooldown.
        /// </summary>
        public static float Cooldown(DynamicBuffer<StatModifier> modifiers, float baseCooldown)
        {
            new StatModifiers(modifiers).Sum(Stat.FireRate, default, out var add, out var percent);
            var multiplier = math.max(MinFireRate, (1f + add * baseCooldown) * (1f + percent));
            return baseCooldown / multiplier;
        }

        // Entities spawned this frame wait for StatBaseSystem, which marks their modifiers changed once bases exist.
        [BurstCompile]
        private struct FieldJob : IJobChunk
        {
            public DynamicComponentTypeHandle Component;
            public int Floats;
            [ReadOnly] public NativeArray<StatField> Fields;
            [ReadOnly] public BufferTypeHandle<StatModifier> Modifiers;
            [ReadOnly] public BufferTypeHandle<BaseStat> Bases;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var values = chunk.GetDynamicComponentDataArrayReinterpret<float>(ref Component, Floats * sizeof(float));
                var modifiers = chunk.GetBufferAccessorRO(ref Modifiers);
                var bases = chunk.GetBufferAccessorRO(ref Bases);
                var entities = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entities.NextEntityIndex(out var i))
                {
                    foreach (var field in Fields)
                    {
                        values[i * Floats + field.Index] = new StatModifiers(modifiers[i]).Apply(bases[i], field.Stat);
                    }
                }
            }
        }

        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        [WithDisabled(typeof(StatBasesPending))]
        private partial struct MaxHealthJob : IJobEntity
        {
            private void Execute(ref Health health, in DynamicBuffer<StatModifier> modifiers,
                in DynamicBuffer<BaseStat> bases)
            {
                var max = new StatModifiers(modifiers).Apply(bases, Stat.MaxHealth);
                health.Current = health.Max > 0f ? health.Current * max / health.Max : max;
                health.Max = max;
            }
        }

        /// <summary>Each weapon keeps its own bases, so the unit's upgrades and veterancy reach every one.</summary>
        [BurstCompile]
        [WithChangeFilter(typeof(StatModifier))]
        [WithDisabled(typeof(StatBasesPending))]
        private partial struct WeaponJob : IJobEntity
        {
            private void Execute(DynamicBuffer<WeaponMount> mounts, in DynamicBuffer<StatModifier> modifiers)
            {
                var stats = new StatModifiers(modifiers);
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    mount.Weapon.Damage.Amount = stats.Evaluate(Stat.Damage, mount.BaseDamage);
                    mount.Weapon.Range = stats.Evaluate(Stat.Range, mount.BaseRange);
                    mount.Weapon.Cooldown = Cooldown(modifiers, mount.BaseCooldown);
                    mounts[i] = mount;
                }
            }
        }
    }
}
