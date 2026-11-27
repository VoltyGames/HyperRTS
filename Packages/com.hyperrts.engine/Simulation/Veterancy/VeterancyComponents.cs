using HyperRTS.Simulation.Stats;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Veterancy;

/// <summary>Experience earned from kills and the rank it reached (0 = none). Only the rank replicates, for the HUD.</summary>
public struct Experience : IComponentData
{
    public float Points;
    [GhostField] public byte Rank;
}

/// <summary>Experience threshold of rank index + 1, ascending.</summary>
[InternalBufferCapacity(3)]
public struct VeterancyRank : IBufferElementData
{
    public float Experience;
}

/// <summary>A modifier active from <see cref="Rank"/> upward.</summary>
[InternalBufferCapacity(0)]
public struct VeterancyBonus : IBufferElementData
{
    public byte Rank;
    public StatModifier Modifier;
}
