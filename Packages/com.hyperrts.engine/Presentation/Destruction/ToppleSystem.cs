using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows a felled piece's remains (<see cref="Felling"/>) tipping from where it stood down to their final pose.
    /// A fall nobody here saw (in fog, or news that came late) shows lying down at once.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct ToppleSystem : ISystem
    {
        private EntityQuery _fog;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _fog = SystemAPI.QueryBuilder().WithAll<FogOfWar>().Build();
            state.RequireForUpdate<Felling>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var view = SystemAPI.TryGetSingleton(out LocalFogView local) ? local : default;
            _fog.CompleteDependency();
            _fog.TryGetSingleton(out FogOfWar fog);
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (felling, transform, entity) in SystemAPI.Query<RefRW<Felling>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                ref var fall = ref felling.ValueRW;
                var started = fall.Time > 0f;
                var shown = started || (fall.Witnessed && Seen(view, fog, transform.ValueRO.Position));
                fall.Time = shown ? fall.Time + deltaTime : ToppleMath.Duration;
                if (fall.Time < ToppleMath.Duration)
                {
                    transform.ValueRW.Rotation = ToppleMath.Pose(fall, fall.Time);
                    continue;
                }

                transform.ValueRW.Rotation = fall.Final;
                SystemAPI.SetComponentEnabled<Felling>(entity, false);
            }
        }

        /// <summary>Whether the local player sees the spot: fog never hides the scenery, but does hide its fall.</summary>
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
