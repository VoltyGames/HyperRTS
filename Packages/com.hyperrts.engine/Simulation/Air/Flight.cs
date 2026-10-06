using Unity.Entities;

namespace HyperRTS.Simulation.Air
{
    /// <summary>How an <c>Air</c>-layer unit flies: cruise height, climb rate, and whether it can hover.</summary>
    public struct Flight : IComponentData
    {
        /// <summary>Cruise height above the ground or water surface.</summary>
        public float Altitude;

        /// <summary>Vertical speed for take-off, landing and following the terrain.</summary>
        public float ClimbSpeed;

        /// <summary>0 hovers when idle; otherwise the aircraft keeps circling at this radius (jets).</summary>
        public float LoiterRadius;
    }
}
