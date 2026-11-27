using HyperRTS.Simulation.Navigation.Heights;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Ambient;

/// <summary>One live dust devil: drifts downwind with a sideways wander, easing in and out over its lifetime.</summary>
public sealed class DustDevil(int id, float3 position, float lifetime, float wanderPhase)
{
    private const float FadeShare = 0.2f;
    private const float WanderShare = 0.5f;
    private const float WanderPeriod = 6f;

    /// <summary>Unique among live devils, so each keeps its own stream.</summary>
    public int Id { get; } = id;

    public float3 Position { get; private set; } = position;
    public float Lifetime { get; } = lifetime;
    public float Age { get; private set; }

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

    /// <summary>Moves along <paramref name="downwind"/> (xz) and onto the ground.</summary>
    public void Step(float deltaTime, float2 downwind, float speed, in TerrainHeight terrain)
    {
        Age += deltaTime;
        var side = new float2(-downwind.y, downwind.x);
        var wander = math.sin(wanderPhase + Age * 2f * math.PI / WanderPeriod) * WanderShare;
        var xz = Position.xz + speed * deltaTime * (downwind + side * wander);
        Position = new float3(xz.x, terrain.Height(xz), xz.y);
    }
}
