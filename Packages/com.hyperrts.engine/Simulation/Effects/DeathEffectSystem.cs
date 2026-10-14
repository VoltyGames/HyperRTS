using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>Plays the death rig of everything <c>DeathSystem</c> marked <see cref="Dead"/> this frame.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct DeathEffectSystem : ISystem
    {
        private EffectWriter _effects;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _effects = new EffectWriter(ref state);
            state.RequireForUpdate<EffectQueue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _effects.Update(ref state, SystemAPI.GetSingletonEntity<EffectQueue>());
            new DeathJob { Effects = _effects }.Schedule();
        }

        // Single-threaded: every death appends to the one effect queue.
        [BurstCompile]
        [WithAll(typeof(Dead), typeof(EntityEffect))]
        private partial struct DeathJob : IJobEntity
        {
            public EffectWriter Effects;

            private void Execute(Entity entity, in LocalTransform transform, in Faction faction) =>
                Effects.Play(entity, EffectSlot.Death, transform, transform.Forward(), faction.Value);
        }
    }
}
