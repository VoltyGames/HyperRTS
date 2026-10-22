using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Burst.Intrinsics;
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
        private EntityQuery _roots;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _switch = new RenderSwitch(ref state);
            _roots = SystemAPI.QueryBuilder().WithAll<EntityInfo, HiddenBy>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _switch.Update(ref state);
            state.Dependency = new SyncJob
            {
                Switch = _switch,
                Entities = SystemAPI.GetEntityTypeHandle(),
                Fog = SystemAPI.GetComponentTypeHandle<FogHidden>(true),
                Inside = SystemAPI.GetComponentTypeHandle<Inside>(true),
                LastSystemVersion = state.LastSystemVersion,
            }.Schedule(_roots, state.Dependency);
        }

        // Gaining or losing FogHidden moves a root to another chunk, so only reordered or boarded chunks can flip.
        [BurstCompile]
        private struct SyncJob : IJobChunk
        {
            public RenderSwitch Switch;
            [ReadOnly] public EntityTypeHandle Entities;
            [ReadOnly] public ComponentTypeHandle<FogHidden> Fog;
            [ReadOnly] public ComponentTypeHandle<Inside> Inside;
            public uint LastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!chunk.DidOrderChange(LastSystemVersion) && !chunk.DidChange(ref Inside, LastSystemVersion))
                {
                    return;
                }

                var entities = chunk.GetNativeArray(Entities);
                var fogged = chunk.Has(ref Fog);
                var carried = chunk.Has(ref Inside);
                for (var i = 0; i < chunk.Count; i++)
                {
                    var aboard = carried && chunk.IsComponentEnabled(ref Inside, i);
                    Sync(entities[i], HideReason.Fog, fogged);
                    Sync(entities[i], HideReason.Inside, aboard);
                }
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
