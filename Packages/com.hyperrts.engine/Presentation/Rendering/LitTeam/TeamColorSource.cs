using HyperRTS.Simulation.Common.Players;
using Unity.Entities;

namespace HyperRTS.Presentation.Rendering.LitTeam
{
    /// <summary>
    /// Paints a root that has no <c>Faction</c> (a visual-only copy such as a fog memory) in a faction's colour,
    /// without gameplay ever counting it as owned.
    /// </summary>
    public struct TeamColorSource : IComponentData
    {
        public byte Faction;
    }
}
