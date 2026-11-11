using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// What a mover reads before it steers and writes after: the crowd round it, its <see cref="Driver"/> and the
    /// handling state it keeps between frames. Opened by <see cref="Movers.Begin"/>, shared by ground and air movers.
    /// </summary>
    internal struct MoveFrame
    {
        public float2 From;
        public float2 Facing;

        /// <summary>Last frame's movement, as the unit's velocity held it.</summary>
        public float2 Velocity;

        /// <summary>Where the planner wants the unit to face; zero leaves it as it is.</summary>
        public float2 Heading;

        /// <summary>Read before arrival can clear it, as the index saw it, so both sides of a contact agree.</summary>
        public bool WasMoving;

        /// <summary>Instant handling needs no memory between frames, so units without state start each from rest.</summary>
        public bool Remembers;

        public CrowdVisitor Crowd;
        public Driver Driver;
    }
}
