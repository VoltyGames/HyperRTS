using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>What a field work does for the bot that digs it in.</summary>
    public enum FieldWorkRole : byte
    {
        /// <summary>Soldiers take cover behind or in it, so it goes across the lane's approach.</summary>
        Cover,

        /// <summary>Slows the enemy down in front of the cover, such as wire.</summary>
        Barrier,
    }

    /// <summary>
    /// Marks a line-placed building the skirmish AI may dig in at its defence points. The bot never takes it as a
    /// build-order step.
    /// </summary>
    public struct AIFieldWork : IComponentData
    {
        public FieldWorkRole Role;
    }
}
