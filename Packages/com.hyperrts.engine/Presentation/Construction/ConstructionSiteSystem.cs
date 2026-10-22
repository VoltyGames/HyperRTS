using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Construction
{
    /// <summary>
    /// Shows a building's <see cref="ConstructionSite"/> dressing while it is built, streams its work rig while progress
    /// keeps rising, and plays its completion rig when a construction this client watched completes.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial class ConstructionSiteSystem : SystemBase
    {
        private readonly EffectStreams _streams = new();
        private EffectPool _pool;
        private RenderSwitch _switch;
        private float _reach;
        private float _deltaTime;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _switch = new RenderSwitch(ref CheckedStateRef);
            RequireForUpdate<ConstructionWatch>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _switch.Update(ref CheckedStateRef);
            var now = SystemAPI.Time.ElapsedTime;
            _deltaTime = SystemAPI.Time.DeltaTime;
            _reach = EffectDetail.Level == DetailLevel.Low ? 0.5f : 1f;
            var progress = SystemAPI.GetComponentLookup<ConstructionProgress>(true);
            var fogged = SystemAPI.GetComponentLookup<FogHidden>(true);
            _streams.BeginFrame();
            foreach (var (site, watch, points, place, entity) in SystemAPI
                         .Query<RefRO<ConstructionSite>, RefRW<ConstructionWatch>, DynamicBuffer<ConstructionWorkPoint>,
                             RefRO<LocalToWorld>>().WithEntityAccess())
            {
                var before = watch.ValueRO.Look;
                var completed = watch.ValueRW.Observe(progress.IsComponentEnabled(entity), progress[entity].Value, now);
                if (watch.ValueRO.Look != before && site.ValueRO.Visual != Entity.Null)
                {
                    _switch.SetBranch(entity, site.ValueRO.Visual, HideReason.Site, !watch.ValueRO.ShowsSite);
                }

                if (fogged.HasComponent(entity))
                {
                    continue;
                }

                if (completed && site.ValueRO.CompleteEffect.IsValid())
                {
                    _pool.Play(site.ValueRO.CompleteEffect.Value, place.ValueRO.Position, place.ValueRO.Rotation);
                }

                if (watch.ValueRO.IsWorking(now) && site.ValueRO.WorkRig.IsValid())
                {
                    Stream(entity, site.ValueRO, points, place.ValueRO);
                }
            }

            _streams.EndFrame();
        }

        private void Stream(Entity entity, in ConstructionSite site, DynamicBuffer<ConstructionWorkPoint> points,
            in LocalToWorld place)
        {
            for (var i = 0; i < points.Length; i++)
            {
                var socket = math.transform(place.Value, points[i].Socket);
                var state = _pool.Step(_streams, site.WorkRig.Value, entity, i, socket, _reach);
                state?.Emit(state.Rig.LoopRotation(place.Rotation, state.Key), _deltaTime, 1f,
                    state.Rig.LoopScale(state.Key));
            }
        }
    }
}
