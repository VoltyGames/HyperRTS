using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Hides roots that are <see cref="FogHidden"/> or riding <see cref="Inside"/> a container, with all their parts.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct FogVisibilitySystem : ISystem
    {
        private RenderSwitch _switch;

        [BurstCompile]
        public void OnCreate(ref SystemState state) => _switch = new RenderSwitch(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _switch.Update(ref state);
            state.Dependency = new SyncJob
            {
                Switch = _switch,
                Fog = SystemAPI.GetComponentLookup<FogHidden>(true),
                Inside = SystemAPI.GetComponentLookup<Inside>(true),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(EntityInfo), typeof(HiddenBy))]
        private partial struct SyncJob : IJobEntity
        {
            public RenderSwitch Switch;
            [ReadOnly] public ComponentLookup<FogHidden> Fog;
            [ReadOnly] public ComponentLookup<Inside> Inside;

            private void Execute(Entity entity)
            {
                var aboard = Inside.HasComponent(entity) && Inside.IsComponentEnabled(entity);
                Sync(entity, HideReason.Fog, Fog.HasComponent(entity));
                Sync(entity, HideReason.Inside, aboard);
            }

            private void Sync(Entity entity, HideReason reason, bool hide)
            {
                if (Switch.IsHidden(entity, reason) != hide)
                {
                    Switch.SetGroup(entity, reason, hide);
                }
            }
        }
    }
}
