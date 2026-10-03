using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Owning player's faction number; 0 is neutral (resource nodes, civilians).</summary>
    public struct Faction : IComponentData
    {
        public const byte Neutral = 0;
        public byte Value;
    }
}
