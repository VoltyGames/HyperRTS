using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>A shot in flight, homing on its hit's target (or its last known position) and hitting on arrival.</summary>
    public struct Projectile : IComponentData
    {
        /// <summary>Keeps a launch straight onto the aim from dividing by zero.</summary>
        private const float MinPath = 0.01f;

        public float Speed;

        /// <summary>Height above the target's pivot that the shot homes on.</summary>
        public float Height;

        /// <summary>Queued on impact; its Position is the aim point, tracked while a homing shot's target lives.</summary>
        public DamageEvent Hit;

        /// <summary>Shooter type whose impact cue (sound, effects, marks) plays on arrival (0 = none); outlives the shooter.</summary>
        public int ImpactTypeId;

        public Trajectory Trajectory;

        /// <summary>Launch point of an arcing shot.</summary>
        public float3 Start;

        /// <summary>Peak height of a ballistic arc, or how high a rocket climbs before it dives.</summary>
        public float Apex;

        /// <summary>0 at launch to 1 on impact along an arcing path.</summary>
        public float Progress;

        public bool Homes => Trajectory != Trajectory.Ballistic;

        public bool Arcs => Trajectory != Trajectory.Direct;

        public bool Arrived => Progress >= 1f;

        /// <summary>Homing shots follow the live target; a ballistic shell loses it once it leaves the aim point.</summary>
        public void Track(float3 targetPosition, float targetRadius)
        {
            if (Homes)
            {
                Hit.Position = targetPosition + new float3(0f, Height, 0f);
                return;
            }

            if (math.distance(targetPosition.xz, Hit.Position.xz) > targetRadius)
            {
                Hit.Target = Entity.Null;
            }
        }

        /// <summary>Moves along the arc at <see cref="Speed"/>; ballistic shells cover their ground distance at it.</summary>
        public void Advance(float deltaTime) =>
            Progress = math.min(1f, Progress + Speed * deltaTime / math.max(PathLength(), MinPath));

        public float3 Position() => Trajectory == Trajectory.Ballistic ? ArcPoint() : CurvePoint();

        /// <summary>Direction of travel, which the shot faces.</summary>
        public float3 Heading() => Trajectory == Trajectory.Ballistic ? ArcTangent() : CurveTangent();

        private float PathLength()
        {
            if (Trajectory == Trajectory.Ballistic)
            {
                return math.distance(Start.xz, Hit.Position.xz);
            }

            // The mean of chord and control polygon is a close, cheap estimate of a cubic's length.
            var chord = math.distance(Start, Hit.Position);
            var polygon = 2f * Apex + math.distance(Start.xz, Hit.Position.xz);
            return 0.5f * (chord + polygon);
        }

        private float3 ArcPoint()
        {
            var t = Progress;
            return math.lerp(Start, Hit.Position, t) + new float3(0f, 4f * Apex * t * (1f - t), 0f);
        }

        private float3 ArcTangent() => Hit.Position - Start + new float3(0f, 4f * Apex * (1f - 2f * Progress), 0f);

        // Control points straight above the launch and the live aim give the climb and the dive.
        private float3 CurvePoint()
        {
            var t = Progress;
            var s = 1f - t;
            var up = new float3(0f, Apex, 0f);
            return s * s * s * Start + 3f * s * s * t * (Start + up) + 3f * s * t * t * (Hit.Position + up)
                + t * t * t * Hit.Position;
        }

        private float3 CurveTangent()
        {
            var t = Progress;
            var s = 1f - t;
            var up = new float3(0f, Apex, 0f);
            return 3f * s * s * up + 6f * s * t * (Hit.Position - Start) - 3f * t * t * up;
        }
    }
}
