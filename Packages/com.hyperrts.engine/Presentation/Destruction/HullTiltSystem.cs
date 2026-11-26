using HyperRTS.Presentation.Animation.Rigs;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Lays each wreck along the ground under its hull when it appears, as the unit it came from stood. The tilt is the
    /// root's <see cref="PostTransformMatrix"/>, so every model under it turns while the simulation's pose stays level.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    // After the buffer that spawns remains, so a wreck lies along its slope from its first frame.
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial struct HullTiltSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HullTilt>();
            state.RequireForUpdate<NavGrid>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var grid = SystemAPI.GetSingleton<NavGrid>();
            foreach (var (tilt, laid, matrix, blocker, place) in SystemAPI
                         .Query<RefRW<HullTilt>, EnabledRefRW<HullTilt>, RefRW<PostTransformMatrix>, RefRO<Blocker>,
                             RefRO<LocalTransform>>())
            {
                var heading = new RigHeading(place.ValueRO);
                var width = blocker.ValueRO.Radius * 2f;
                var footprint = new float2(math.max(blocker.ValueRO.HullLength, width), width);
                tilt.ValueRW.Value = MotionRig.GroundTilt(grid, footprint, heading.Position, heading.Forward, heading.Right);
                // A matrix already there is the tilt a ruin took over from the wreck it replaces, so it is written over.
                matrix.ValueRW.Value = new float4x4(Turn(place.ValueRO.Rotation, heading, tilt.ValueRO.Value), float3.zero);
                laid.ValueRW = false;
            }
        }

        // A root the map already leaned along its slope turns only by what is left.
        private static quaternion Turn(quaternion rotation, in RigHeading heading, float2 tilt)
        {
            var level = quaternion.LookRotation(heading.Forward, math.up());
            return math.mul(math.inverse(rotation), math.mul(level, MotionRig.TiltRotation(tilt)));
        }
    }
}
