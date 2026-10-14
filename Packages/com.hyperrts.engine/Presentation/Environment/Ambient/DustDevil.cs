using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>One live dust devil: drifts downwind with a sideways wander, easing in and out over its lifetime.</summary>
    public sealed class DustDevil
    {
        private const float FadeShare = 0.2f;
        private const float WanderShare = 0.5f;
        private const float WanderPeriod = 6f;

        private readonly float _wanderPhase;

        public DustDevil(float3 position, float lifetime, float wanderPhase)
        {
            Position = position;
            Lifetime = lifetime;
            _wanderPhase = wanderPhase;
        }

        public float3 Position { get; private set; }
        public float Lifetime { get; }
        public float Age { get; private set; }

        /// <summary>Leftover particle fraction between frames, for <c>EffectRig.Stream</c>.</summary>
        public float Carry;

        public bool Done => Age >= Lifetime;

        /// <summary>0 → 1 over the first fifth of the lifetime, back to 0 over the last.</summary>
        public float Strength
        {
            get
            {
                var edge = math.min(Age, Lifetime - Age) / (Lifetime * FadeShare);
                return math.smoothstep(0f, 1f, edge);
            }
        }

        /// <summary>Moves along <paramref name="downwind"/> (xz) and onto the ground; returns where it was.</summary>
        public float3 Step(float deltaTime, float2 downwind, float speed, in TerrainHeight terrain)
        {
            var from = Position;
            Age += deltaTime;
            var side = new float2(-downwind.y, downwind.x);
            var wander = math.sin(_wanderPhase + Age * 2f * math.PI / WanderPeriod) * WanderShare;
            var xz = from.xz + (downwind + side * wander) * speed * deltaTime;
            Position = new float3(xz.x, terrain.Height(xz), xz.y);
            return from;
        }
    }
}
