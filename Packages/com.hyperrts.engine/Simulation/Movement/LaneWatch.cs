using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// What one unit sees of vehicle lanes in a frame: a foot unit's step out of an ally's, a parked vehicle's way
    /// out of one, and who stands in a moving vehicle's own.
    /// </summary>
    internal struct LaneWatch
    {
        /// <summary>A vehicle done waiting on allied foot units, which no longer hold it until it is clear of them.</summary>
        public bool PassesInfantry;

        /// <summary>An allied foot unit stands in this moving vehicle's lane, so it slows to a walk.</summary>
        public bool InfantryAhead;

        /// <summary>The move that takes this parked vehicle out of the lanes of allied movers; zero when it stands in none.</summary>
        public float2 WayOut;

        private bool _infantryTouches;
        private bool _othersTouch;
        private bool _infantryNear;

        /// <summary>Allied foot units stand in its lane, or it is driving through them, so it keeps to a walk.</summary>
        public readonly bool WalksForInfantry => InfantryAhead || (PassesInfantry && _infantryNear);

        /// <summary>Allied foot units alone stop this vehicle: one touches it in its lane and nothing else does.</summary>
        public readonly bool IsHeldByInfantry => _infantryTouches && !_othersTouch;

        /// <summary>An allied foot unit is within this vehicle's spacing, so it is not yet clear of them.</summary>
        public readonly bool IsNearInfantry => _infantryNear;

        public void SeeInfantryNear(bool near) => _infantryNear |= near;

        /// <summary>
        /// Looks at <paramref name="entry"/> for <paramref name="unit"/> and returns the push that steps a foot unit
        /// out of its lane. A parked vehicle is never pushed out: it notes its <see cref="WayOut"/> and drives.
        /// </summary>
        public float2 Watch(in RightOfWay rule, in SpatialEntry unit, in SpatialEntry entry, bool touching,
            bool entryYields)
        {
            if (!unit.IsVehicle || !unit.IsMoving)
            {
                var clears = entry.IsVehicle && entry.IsMoving && rule.Yields(unit, entry);
                var step = clears ? rule.Lane.StepAside(entry, unit) : float2.zero;
                WayOut += unit.IsVehicle ? step : float2.zero;
                return unit.IsVehicle ? float2.zero : step;
            }

            if (!rule.Lane.Holds(unit, entry))
            {
                return float2.zero;
            }

            var infantry = !entry.IsVehicle && entryYields;
            InfantryAhead |= infantry;
            _infantryTouches |= infantry && touching;
            _othersTouch |= !infantry && touching;
            return float2.zero;
        }
    }
}
