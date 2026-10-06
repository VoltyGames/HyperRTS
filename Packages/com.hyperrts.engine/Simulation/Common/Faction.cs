using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Owning player's faction number; 0 is neutral (resource nodes, civilians).</summary>
    public struct Faction : IComponentData
    {
        public const byte Neutral = 0;
        [GhostField] public byte Value;
    }
}
