using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Distance to the nearest wall at any point, for loops that must run over either the field or a copy of it.</summary>
    public interface IWallDistance
    {
        float Distance(float2 position);
    }
}
