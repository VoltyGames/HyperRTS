using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Death;

/// <summary>Who last damaged this entity; read on death for kill credit.</summary>
public struct LastAttacker : IComponentData
{
    public Entity Source;
    public byte Owner;

    /// <summary>Whether killing <paramref name="victim"/>'s entity earns kill credit: allies and self give none.</summary>
    public readonly bool CreditsKill(in OwnerRelations relations, byte victim) => relations.IsHostile(Owner, victim);
}

/// <summary>The remains spawned where the entity dies (wreck, ruin, fallen tree).</summary>
public struct SpawnsRemains : IComponentData
{
    public Entity Prefab;
}

/// <summary>Marks what a death left behind: a wreck, a ruin, a fallen tree, a thrown turret.</summary>
public struct RemainsTag : IComponentData { }

/// <summary>
/// On fresh remains until presentation has placed their parts, which keep their baked places until the transform
/// systems next run, a frame after the remains are first drawn.
/// </summary>
public struct Unplaced : IComponentData, IEnableableComponent { }

/// <summary>Remains falling to the ground under gravity; removed when they land.</summary>
public struct FallingRemains : IComponentData
{
    public float Speed;

    /// <summary>Drops <paramref name="height"/> one step; true once it reaches <paramref name="ground"/>.</summary>
    public bool Fall(ref float height, float ground, float deltaTime)
    {
        Speed += Gravity.Acceleration * deltaTime;
        height -= Speed * deltaTime;
        if (height > ground)
        {
            return false;
        }

        height = ground;
        return true;
    }
}

/// <summary>Remains that last <see cref="Lifetime"/> seconds, then are buried <see cref="DespawnDepth"/> metres deep and go.</summary>
public struct Decay : IComponentData
{
    public float Lifetime;

    public float DespawnDuration;

    public float DespawnDepth;

    /// <summary>Elapsed time the despawn starts, set by the authoritative world on its first look; 0 until then.</summary>
    public double DespawnStartTime;

    /// <summary>Share of the despawn done, 0 to 1. Replicated so clients fade the remains' loop effects with it.</summary>
    [GhostField(Quantization = 100)] public float DespawnProgress;
}
