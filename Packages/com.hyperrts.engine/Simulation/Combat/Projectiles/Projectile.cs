using HyperRTS.Simulation.Combat.Damage;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Projectiles
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

        /// <summary>How it flies, copied from its weapon at launch.</summary>
        public ShotCourse Course;

        /// <summary>What an <see cref="Interceptor"/> sees it as.</summary>
        public ProjectileKind Kind;

        /// <summary>Shot down this frame: it stops flying and never lands.</summary>
        public bool Intercepted;

        /// <summary>Launch point of an arcing shot.</summary>
        public float3 Start;

        /// <summary>Peak height of a ballistic arc, or how high a rocket climbs before it dives.</summary>
        public float Apex;

        /// <summary>0 at launch to 1 on impact along an arcing path.</summary>
        public float Progress;

        public bool Homes => Course.Homes;

        public bool Arcs => Course.Curves;

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

        public float3 Position() => Course.Point(Start, Hit.Position, Apex, Progress);

        /// <summary>Direction of travel, which the shot faces.</summary>
        public float3 Heading() => Course.Tangent(Start, Hit.Position, Apex, Progress);

        private float PathLength() => Course.PathLength(Start, Hit.Position, Apex);
    }
}
