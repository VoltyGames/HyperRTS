using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// A ground vehicle's body on its springs: it squats as it speeds up, dives as it brakes, rolls out of turns and
    /// rocks back from its own shots. Aircraft have a zero one. Pitch is nose up and roll right side up, in radians.
    /// </summary>
    public struct Suspension
    {
        // Longer frames split so the spring stays stable through a hitch.
        private const float MaxStep = 1f / 30f;

        /// <summary>Spring frequency in hertz and damping ratio, where 1 settles without overshoot.</summary>
        public float Frequency;
        public float Damping;

        /// <summary>Radians of sway per m/s² of acceleration, and the most it sways.</summary>
        public float PerAcceleration;
        public float MaxSway;

        /// <summary>Radians per second a shot rocks the body away from where the gun points.</summary>
        public float RecoilRate;

        public float2 Sway;
        public float2 Rate;

        /// <summary>The lean the springs settle at under <paramref name="acceleration"/>.</summary>
        public readonly float2 Target(float3 acceleration, float3 forward, float3 right)
        {
            var sway = new float2(math.dot(acceleration, forward), math.dot(acceleration, right)) * PerAcceleration;
            return math.clamp(sway, -MaxSway, MaxSway);
        }

        /// <summary>Springs <see cref="Sway"/> toward <paramref name="target"/> over <paramref name="deltaTime"/>.</summary>
        public void Spring(float2 target, float deltaTime)
        {
            var omega = 2f * math.PI * Frequency;
            var steps = (int)math.ceil(deltaTime / MaxStep);
            var step = deltaTime / steps;
            for (var i = 0; i < steps; i++)
            {
                Rate += (omega * omega * (target - Sway) - 2f * Damping * omega * Rate) * step;
                Sway += Rate * step;
            }

            Sway = math.clamp(Sway, -MaxSway, MaxSway);
        }

        /// <summary>A shot from a gun turned <paramref name="yaw"/> radians right of the nose.</summary>
        public void Recoil(float yaw) => Rate += RecoilRate * new float2(math.cos(yaw), math.sin(yaw));
    }
}
