using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>A <see cref="Locomotion"/> vehicle's motion between frames. Its facing is its rotation.</summary>
    public struct LocomotionState : IComponentData
    {
        /// <summary>Metres per second along the facing; negative while reversing.</summary>
        public float Speed;

        /// <summary>Signed curvature the wheels steer, in 1/m, positive to the left; 0 for tracks.</summary>
        public float Steer;

        /// <summary>Backing up to a goal close behind rather than turning round.</summary>
        public bool Reverse;

        /// <summary>Going the other way to bring a target out of its turning circle, as in a three-point turn.</summary>
        public bool InThreePointTurn;

        /// <summary>Seconds left driving the other way after a wheeled leg ran head-on into a wall.</summary>
        public float OtherWay;

        /// <summary>The velocity <see cref="Steering"/> chose last frame, which it prefers to keep.</summary>
        public float2 LastChoice;

        /// <summary>In a manoeuvre that steering would break: a three-point turn or backing off a wall.</summary>
        public readonly bool IsManoeuvring => InThreePointTurn || OtherWay > 0f;

        /// <summary>Last frame's move was stopped head-on by a wall.</summary>
        public bool Blocked;

        /// <summary>
        /// Seconds allied foot units alone have held it. From <see cref="PassAfter"/> it drives through them, at a walk,
        /// until it is clear of them.
        /// </summary>
        public float InfantryWait;

        public readonly bool PassesInfantry => InfantryWait >= PassAfter;

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

        /// <summary>Counts how long allied foot units alone have held it, and forgets once it is clear of them.</summary>
        public void WaitOnInfantry(bool held, bool clear, float moved, float deltaTime)
        {
            if (clear)
            {
                InfantryWait = 0f;
                return;
            }

            if (PassesInfantry)
            {
                return;
            }

            var standing = held && moved < HeldSpeed * deltaTime;
            InfantryWait = standing ? InfantryWait + deltaTime : 0f;
        }

        // Below this share of the planned step the hit is head-on, not glancing.
        private const float GlancingShare = 0.5f;

        // Long enough for foot units with room to step aside to have done so.
        private const float PassAfter = 2f;

        // Slower than a crawl counts as held.
        private const float HeldSpeed = 0.3f;
    }
}
