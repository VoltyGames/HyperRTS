using HyperRTS.Simulation.Common.Players;
using Unity.Entities;

namespace HyperRTS.Presentation.Rendering.Materials
{
    /// <summary>Faction whose colour was last applied to this root; client-only bookkeeping.</summary>
    public struct TeamColored : IComponentData
    {
        public byte Faction;
    }
}
