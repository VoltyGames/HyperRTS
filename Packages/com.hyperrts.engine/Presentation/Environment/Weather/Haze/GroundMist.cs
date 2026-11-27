using Unity.Entities;

namespace HyperRTS.Presentation.Environment.Weather.Haze;

/// <summary>How much mist lies in low ground (0 none, 1 thick), as a singleton the <see cref="Atmosphere"/> writes; absent means none.</summary>
public struct GroundMist : IComponentData
{
    public float Level;
}
