using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows felled remains tipping down to the final pose the simulation already gave them, through their rendered
    /// transform only. Unseen falls show lying down at once.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(UpdatePresentationSystemGroup))]
    public partial struct ToppleSystem : ISystem
    {
        private EntityQuery _fog;
        private EntityQuery _felled;
        private EntityQuery _falling;
        private RenderPose _pose;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _fog = SystemAPI.QueryBuilder().WithAll<FogOfWar>().Build();
            _felled = SystemAPI.QueryBuilder().WithAll<Felling>().WithAbsent<FallClock>().Build();
            _falling = SystemAPI.QueryBuilder().WithAll<Felling, FallClock, LocalTransform, LocalToWorld>().Build();
            _pose = new RenderPose(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!_felled.IsEmpty)
            {
                state.EntityManager.AddComponent<FallClock>(_felled);
            }

            // The clock's enable bit ends the work, so resting remains don't keep the fog synced.
            if (_falling.IsEmpty)
            {
                return;
            }

            var view = SystemAPI.TryGetSingleton(out LocalFogView local) ? local : default;
            state.CompleteDependency();
            _fog.TryGetSingleton(out FogOfWar fog);
            _pose.Update(ref state);
            var deltaTime = SystemAPI.Time.DeltaTime;
            using var entities = _falling.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var felling = SystemAPI.GetComponent<Felling>(entity);
                var clock = SystemAPI.GetComponentRW<FallClock>(entity);
                var started = clock.ValueRO.Time > 0f;
                var shown = started || (felling.Witnessed && Seen(view, fog, _pose.Position(entity)));
                clock.ValueRW.Time = shown ? clock.ValueRO.Time + deltaTime : ToppleMath.Duration;
                var final = _pose.Rotation(entity);
                if (clock.ValueRO.Time < ToppleMath.Duration)
                {
                    _pose.Show(entity, ToppleMath.Pose(felling, final, clock.ValueRO.Time));
                    continue;
                }

                // A tipped one may hold a stale tipped matrix, since the transform systems only redo changed ones.
                if (started)
                {
                    _pose.Show(entity, final);
                }

                SystemAPI.SetComponentEnabled<FallClock>(entity, false);
            }
        }

        /// <summary>Fog never hides scenery, but it does hide the fall.</summary>
        private static bool Seen(in LocalFogView view, in FogOfWar fog, float3 position)
        {
            if (!view.Active || view.RevealAll || !fog.IsCreated)
            {
                return true;
            }

            return fog.IsVisible(position, view.Team);
        }
    }
}
