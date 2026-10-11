using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Production
{
    /// <summary>
    /// Base for the authoring of anything a producer makes (units, buildings, research): the type identity that
    /// prerequisites and production options refer to.
    /// </summary>
    public abstract class ProducibleAuthoring : AuthoringBehaviour
    {
        /// <summary>Name shown in the HUD; also the type's identity.</summary>
        public abstract string DisplayName { get; }

        public int TypeId => EntityInfo.TypeIdFromName(DisplayName);
    }
}
