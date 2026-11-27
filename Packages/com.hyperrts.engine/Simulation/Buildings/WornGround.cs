using Unity.Entities;

namespace HyperRTS.Simulation.Buildings;

/// <summary>A band of trodden mud round a finished building's footprint. Only clients draw it.</summary>
public struct WornGround : IComponentData
{
    /// <summary>Metres the band reaches out from the footprint's edge.</summary>
    public float Width;

    /// <summary>How thick the mud lies, 0 to 1.</summary>
    public float Strength;
}
