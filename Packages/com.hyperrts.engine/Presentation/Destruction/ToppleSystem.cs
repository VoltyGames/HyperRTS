using HyperRTS.Core;
using HyperRTS.Presentation.Animation;
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
    /// transform only. A crushing vehicle's speed pushes the fall on. Unseen falls show lying down at once.
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
        private EntityQuery _crushers;
        private RenderPose _pose;

        // How far from the stump a crusher counts as the one pushing the tree over.
        private const float CrushReach = 6f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _fog = SystemAPI.QueryBuilder().WithAll<FogOfWar>().Build();
            _felled = SystemAPI.QueryBuilder().WithAll<ToppleFall>().WithAbsent<FallClock>().Build();
            _falling = SystemAPI.QueryBuilder().WithAll<ToppleFall, FallClock, LocalTransform, LocalToWorld>().Build();
            _crushers = SystemAPI.QueryBuilder().WithAll<Crusher, MotionRig, LocalToWorld>().Build();
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
                var toppleFall = SystemAPI.GetComponent<ToppleFall>(entity);
                var clock = SystemAPI.GetComponentRW<FallClock>(entity);
                if (!clock.ValueRO.Started)
                {
                    var shown = toppleFall.Witnessed && Seen(view, fog, _pose.Position(entity));
                    if (!shown)
                    {
                        SystemAPI.SetComponentEnabled<FallClock>(entity, false);
                        continue;
                    }

                    clock.ValueRW = FallClock.Pushed(toppleFall.Height, CrushSpeed(_pose.Position(entity)));
                }

                clock.ValueRW.Advance(toppleFall.Height, deltaTime);
                var final = _pose.Rotation(entity);
                if (!clock.ValueRO.Resting)
                {
                    _pose.Show(entity, ToppleMath.Pose(toppleFall, final, clock.ValueRO.Progress));
                    continue;
                }

                // The tipped matrix is stale, since the transform systems only redo changed ones.
                _pose.Show(entity, final);
                SystemAPI.SetComponentEnabled<FallClock>(entity, false);
            }
        }

        /// <summary>Speed in m/s of the fastest crusher at the stump, or 0 if none is there.</summary>
        private float CrushSpeed(float3 stump)
        {
            using var rigs = _crushers.ToComponentDataArray<MotionRig>(Allocator.Temp);
            using var places = _crushers.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            var speed = 0f;
            for (var i = 0; i < rigs.Length; i++)
            {
                var near = math.distancesq(places[i].Position.xz, stump.xz) < CrushReach * CrushReach;
                speed = near ? math.max(speed, math.length(rigs[i].LastVelocity.xz)) : speed;
            }

            return speed;
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
