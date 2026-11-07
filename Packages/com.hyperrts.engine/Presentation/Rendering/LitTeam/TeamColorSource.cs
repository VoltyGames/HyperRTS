using HyperRTS.Simulation.Common.Players;
using Unity.Entities;

namespace HyperRTS.Presentation.Rendering.LitTeam
{
    /// <summary>
    /// Paints a root that has no <c>Owner</c> (a visual-only copy such as a fog memory) in an owner's colour,
    /// without gameplay ever counting it as owned.
    /// </summary>
    public struct TeamColorSource : IComponentData
    {
        public byte Owner;
    }
}
