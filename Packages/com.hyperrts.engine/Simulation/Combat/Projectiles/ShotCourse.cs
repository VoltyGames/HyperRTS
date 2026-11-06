using System;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Projectiles
{
    /// <summary>
    /// How a shot flies: whether it homes, whether it curves and the cubic it follows, and whether it needs a clear
    /// line. A <see cref="Trajectory"/> picks the preset a weapon authors; runtime code reads only this.
    /// </summary>
    public struct ShotCourse
    {
        /// <summary>Follows the live target to its end; otherwise it flies to the point aimed at launch.</summary>
        public bool Homes;

        /// <summary>Follows a cubic from launch to aim from launch to aim; otherwise it chases the aim in a straight line.</summary>
        public bool Curves;

        /// <summary>Hits whatever stands in the way, so it is only fired along a clear line.</summary>
        public bool NeedsLineOfFire;

        /// <summary>Covers its ground distance at its speed, rather than the length of its curve.</summary>
        public bool PacedOnGround;

        /// <summary>How far along the launch-to-aim chord, as a fraction from each end, the two control points sit.</summary>
        public float Reach;

        /// <summary>How high the control points sit, as a multiple of the apex.</summary>
        public float Lift;

        /// <summary>Straight and homing.</summary>
        public static ShotCourse Direct => new() { Homes = true, NeedsLineOfFire = true };

        /// <summary>An arc onto the point aimed at launch (artillery): control points at the chord's thirds, so the apex is the one given.</summary>
        public static ShotCourse Ballistic => new() { Curves = true, PacedOnGround = true, Reach = 1f / 3f, Lift = 4f / 3f };

        /// <summary>A steep climb then a guided dive: control points over the launch and the aim.</summary>
        public static ShotCourse Rocket => new() { Homes = true, Curves = true, NeedsLineOfFire = true, Lift = 1f };

        public static ShotCourse From(Trajectory trajectory) => trajectory switch
        {
            Trajectory.Direct => Direct,
            Trajectory.Ballistic => Ballistic,
            Trajectory.Rocket => Rocket,
            _ => throw new ArgumentOutOfRangeException(nameof(trajectory), trajectory, null),
        };

        /// <summary>Where the shot is a fraction <paramref name="t"/> of the way along its curve.</summary>
        public readonly float3 Point(float3 start, float3 end, float apex, float t)
        {
            Controls(start, end, apex, out var first, out var second);
            var s = 1f - t;
            return s * s * s * start + 3f * s * s * t * first + 3f * s * t * t * second + t * t * t * end;
        }

        /// <summary>The direction of travel at <paramref name="t"/>, not normalised.</summary>
        public readonly float3 Tangent(float3 start, float3 end, float apex, float t)
        {
            Controls(start, end, apex, out var first, out var second);
            var s = 1f - t;
            return 3f * s * s * (first - start) + 6f * s * t * (second - first) + 3f * t * t * (end - second);
        }

        /// <summary>How far the shot travels at its speed.</summary>
        public readonly float PathLength(float3 start, float3 end, float apex)
        {
            var ground = math.distance(start.xz, end.xz);
            if (PacedOnGround)
            {
                return ground;
            }

            // The mean of chord and control polygon is a close, cheap estimate of a cubic's length.
            return 0.5f * (math.distance(start, end) + 2f * Lift * apex + ground);
        }

        private readonly void Controls(float3 start, float3 end, float apex, out float3 first, out float3 second)
        {
            var chord = end - start;
            var lift = new float3(0f, Lift * apex, 0f);
            first = start + chord * Reach + lift;
            second = end - chord * Reach + lift;
        }
    }
}
