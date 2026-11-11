using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>
    /// Hides roots riding <see cref="Inside"/> a container and fades roots in and out of the fog over <see cref="FadeTime"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct FogVisibilitySystem : ISystem
    {
        public const float FadeTime = 0.25f;

        private RenderSwitch _switch;
        private MeshPainter<RevealFade> _painter;
        private EntityQuery _roots;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _switch = new RenderSwitch(ref state);
            _painter = new MeshPainter<RevealFade>(ref state);
            _roots = SystemAPI.QueryBuilder().WithAll<EntityInfo, HiddenBy>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _switch.Update(ref state);
            state.Dependency = new InsideJob
            {
                Switch = _switch,
                Entities = SystemAPI.GetEntityTypeHandle(),
                Inside = SystemAPI.GetComponentTypeHandle<Inside>(true),
                LastSystemVersion = state.LastSystemVersion,
            }.Schedule(_roots, state.Dependency);
            state.CompleteDependency();

            var parts = new NativeList<Entity>(Allocator.Temp);
            var commands = new EntityCommandBuffer(Allocator.Temp);
            _painter.Update(ref state);
            HideUnseen(ref state);
            Arrive(ref state, parts, commands);
            Leave(ref state, commands);
            commands.Playback(state.EntityManager);
            commands.Dispose();

            // After playback, so the painter and switch see the meshes and roots that just changed.
            commands = new EntityCommandBuffer(Allocator.Temp);
            _switch.Update(ref state);
            _painter.Update(ref state);
            Fade(ref state, parts, commands, SystemAPI.Time.DeltaTime / FadeTime);
            commands.Playback(state.EntityManager);
        }

        private void HideUnseen(ref SystemState state)
        {
            foreach (var root in SystemAPI.QueryBuilder().WithAll<FogHidden, EntityInfo, HiddenBy>()
                         .WithNone<FogShown>().Build().ToEntityArray(Allocator.Temp))
            {
                if (!_switch.IsHidden(root, HideReason.Fog))
                {
                    _switch.SetGroup(root, HideReason.Fog, true);
                }
            }
        }

        // Map scenery is never fogged, so it never needs the fade's property.
        private void Arrive(ref SystemState state, NativeList<Entity> parts, EntityCommandBuffer commands)
        {
            foreach (var root in SystemAPI.QueryBuilder().WithAll<EntityInfo, HiddenBy>()
                         .WithNone<FogHidden, FogShown, Destructible>().Build().ToEntityArray(Allocator.Temp))
            {
                var hidden = _switch.IsHidden(root, HideReason.Fog);
                if (hidden)
                {
                    _switch.SetGroup(root, HideReason.Fog, false);
                    commands.AddComponent(root, new FogFading { Reveal = 0f });
                }

                _painter.Collect(root, parts);
                _painter.Add(parts, new RevealFade { Value = hidden ? 0f : 1f }, commands);
                commands.AddComponent<FogShown>(root);
            }
        }

        private void Leave(ref SystemState state, EntityCommandBuffer commands)
        {
            foreach (var root in SystemAPI.QueryBuilder().WithAll<FogHidden, FogShown>().WithNone<FogFading>().Build()
                         .ToEntityArray(Allocator.Temp))
            {
                commands.AddComponent(root, new FogFading { Reveal = 1f });
            }
        }

        private void Fade(ref SystemState state, NativeList<Entity> parts, EntityCommandBuffer commands, float step)
        {
            foreach (var (fade, root) in SystemAPI.Query<RefRW<FogFading>>().WithAll<FogShown>().WithEntityAccess())
            {
                var leaving = SystemAPI.HasComponent<FogHidden>(root);
                var reveal = math.saturate(fade.ValueRO.Reveal + (leaving ? -step : step));
                fade.ValueRW.Reveal = reveal;
                _painter.Collect(root, parts);
                _painter.Set(parts, new RevealFade { Value = reveal });
                if (leaving && reveal <= 0f)
                {
                    _switch.SetGroup(root, HideReason.Fog, true);
                    commands.RemoveComponent<FogShown>(root);
                    commands.RemoveComponent<FogFading>(root);
                }
                else if (!leaving && reveal >= 1f)
                {
                    commands.RemoveComponent<FogFading>(root);
                }
            }
        }

        // Boarding flips Inside's enable bit, so only chunks it changed in can flip.
        [BurstCompile]
        private struct InsideJob : IJobChunk
        {
            public RenderSwitch Switch;
            [ReadOnly] public EntityTypeHandle Entities;
            [ReadOnly] public ComponentTypeHandle<Inside> Inside;
            public uint LastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!chunk.DidOrderChange(LastSystemVersion) && !chunk.DidChange(ref Inside, LastSystemVersion))
                {
                    return;
                }

                var entities = chunk.GetNativeArray(Entities);
                var carried = chunk.Has(ref Inside);
                for (var i = 0; i < chunk.Count; i++)
                {
                    var aboard = carried && chunk.IsComponentEnabled(ref Inside, i);
                    if (Switch.IsHidden(entities[i], HideReason.Inside) != aboard)
                    {
                        Switch.SetGroup(entities[i], HideReason.Inside, aboard);
                    }
                }
            }
        }
    }
}
