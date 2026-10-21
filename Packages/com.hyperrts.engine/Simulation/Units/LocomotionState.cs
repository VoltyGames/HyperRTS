using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>A <see cref="Locomotion"/> vehicle's motion between frames. Its facing is its rotation.</summary>
    public struct LocomotionState : IComponentData
    {
        /// <summary>Metres per second along the facing; negative while reversing.</summary>
        public float Speed;

        /// <summary>Backing up to a goal close behind rather than turning round.</summary>
        public bool Reverse;

        /// <summary>Going the other way to bring a target out of its turning circle, as in a three-point turn.</summary>
        public bool InThreePointTurn;

        /// <summary>Seconds left driving the turn's other leg after the reversing leg ran into something.</summary>
        public float ThreePointTurnBlocked;

        /// <summary>Last frame's move was stopped head-on by a wall.</summary>
        public bool Blocked;

        /// <summary>
        /// Loses the speed a wall stopped it covering, so it doesn't spring off at full speed once clear. Sliding
        /// along a wall at a glancing angle keeps its speed. The crowd's push costs none, so a jam can't pin it.
        /// </summary>
        public void Hit(float2 moved, float2 step, bool walled)
        {
            var planned = math.lengthsq(step);
            var covered = walled && planned > 0f ? math.saturate(math.dot(moved, step) / planned) : 1f;
            Blocked = covered < GlancingShare;
            if (Blocked)
            {
                Speed *= covered;
            }
        }

        // Below this share of the planned step the hit is head-on, not glancing.
        private const float GlancingShare = 0.5f;
    }
}
