using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// The lane ahead of a moving vehicle: its hull's width with spacing, from its centre to its braking distance
    /// past the nose and a lead, and no further than its goal. Allied foot units and parked idle vehicles leave it,
    /// a squad by as far as its members stand out and a hull by its length.
    /// </summary>
    public struct VehicleLane
    {
        // Past the braking distance, so a soldier has time at a walk to clear the lane before contact.
        private const float Lead = 4f;

        // A foot unit steps this far past the lane's edge, so it doesn't stand on it.
        private const float Margin = 0.1f;

        // Slower than this a vehicle's lane runs along its facing, as its velocity has no direction.
        private const float Rolling = 0.1f;

        [ReadOnly] private ComponentLookup<Locomotion> _handling;

        public VehicleLane(ref SystemState state)
        {
            _handling = state.GetComponentLookup<Locomotion>(true);
        }

        public void Update(ref SystemState state)
        {
            _handling.Update(ref state);
        }

        /// <summary>Whether <paramref name="unit"/> stands in the lane ahead of <paramref name="vehicle"/>.</summary>
        public readonly bool Holds(in SpatialEntry vehicle, in SpatialEntry unit) =>
            Depth(vehicle, unit, Travel(vehicle)) > 0f;

        /// <summary>
        /// The move that takes <paramref name="unit"/> out of the lane ahead of <paramref name="vehicle"/>: to the
        /// side it leans to and as far on ahead, so one a wall or a second lane keeps in walks on in front.
        /// </summary>
        public readonly float2 StepAside(in SpatialEntry vehicle, in SpatialEntry unit)
        {
            var travel = Travel(vehicle);
            var depth = Depth(vehicle, unit, travel);
            if (depth <= 0f)
            {
                return float2.zero;
            }

            var side = RightOfWay.Side(travel, unit.Position.xz - vehicle.Position.xz);
            return (side + travel) * (depth + Margin);
        }

        // Metres from the unit to the lane's nearer edge; zero or less outside the lane.
        private readonly float Depth(in SpatialEntry vehicle, in SpatialEntry unit, float2 travel)
        {
            if (vehicle.IsGivingWay)
            {
                return 0f;
            }

            var offset = unit.Position.xz - vehicle.Position.xz;
            var reach = unit.Radius + math.length(unit.SegmentAxis) + unit.Spread;
            var along = math.dot(offset, travel);
            var length = vehicle.HullReach + math.min(Ahead(vehicle), vehicle.GoalDistance);
            if (along <= 0f || along > length + reach)
            {
                return 0f;
            }

            var halfWidth = vehicle.Radius + reach + math.max(vehicle.Spacing, unit.Spacing);
            return halfWidth - math.abs(Planar.Cross(travel, offset));
        }

        private static float2 Travel(in SpatialEntry vehicle)
        {
            var speedSq = math.lengthsq(vehicle.Velocity);
            return speedSq > Rolling * Rolling ? vehicle.Velocity * math.rsqrt(speedSq) : vehicle.Facing;
        }

        private readonly float Ahead(in SpatialEntry vehicle) =>
            math.lengthsq(vehicle.Velocity) / (2f * _handling[vehicle.Entity].Braking) + Lead;
    }
}
