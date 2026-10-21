using HyperRTS.Simulation.Combat;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>One thrown debris piece: a ballistic flight that bounces off the terrain, lies flat, then shrinks away.</summary>
    public struct DebrisPiece
    {
        /// <summary>Share of the fall speed kept on a bounce, and of the slide and spin.</summary>
        public const float Bounce = 0.35f;
        public const float Friction = 0.55f;

        /// <summary>Landing slower than this in m/s, the piece stops where it is.</summary>
        public const float RestSpeed = 1f;

        /// <summary>Seconds at the end of its life the piece shrinks over.</summary>
        public const float FadeTime = 2f;

        public float3 Position;
        public float3 Velocity;

        /// <summary>Rotation axis times radians per second.</summary>
        public float3 Spin;
        public quaternion Rotation;

        /// <summary>Scale per axis applied to the mesh.</summary>
        public float3 Size;

        /// <summary>Metres from its centre down to the ground when it lies flat.</summary>
        public float HalfHeight;
        public int Mesh;
        public float Age;
        public float Life;
        public bool Resting;

        public readonly bool Gone => Age >= Life;

        /// <summary>1 for most of its life, down to 0 as it goes.</summary>
        public readonly float Shrink => math.saturate((Life - Age) / FadeTime);

        /// <summary>Flies on by <paramref name="deltaTime"/> over terrain <paramref name="ground"/> metres high.</summary>
        public void Step(float deltaTime, float ground)
        {
            Age += deltaTime;
            if (Resting)
            {
                return;
            }

            Velocity.y -= FallingRemains.Gravity * deltaTime;
            Position += Velocity * deltaTime;
            var turn = math.length(Spin) * deltaTime;
            if (turn > 0f)
            {
                Rotation = math.normalize(math.mul(quaternion.AxisAngle(math.normalize(Spin), turn), Rotation));
            }

            var floor = ground + HalfHeight;
            if (Position.y > floor)
            {
                return;
            }

            Position.y = floor;
            if (-Velocity.y < RestSpeed)
            {
                Settle();
                return;
            }

            Velocity = new float3(Velocity.x * Friction, -Velocity.y * Bounce, Velocity.z * Friction);
            Spin *= Friction;
        }

        // Lies flat along its heading, so a plank never rests on its end.
        private void Settle()
        {
            Resting = true;
            Velocity = float3.zero;
            Spin = float3.zero;
            var forward = math.rotate(Rotation, math.forward());
            Rotation = quaternion.RotateY(math.atan2(forward.x, forward.z));
        }
    }
}
