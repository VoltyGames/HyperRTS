using Unity.Entities;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>Experience earned from kills and the rank it reached (0 = none).</summary>
    public struct Experience : IComponentData
    {
        public float Points;
        public byte Rank;
    }
}
