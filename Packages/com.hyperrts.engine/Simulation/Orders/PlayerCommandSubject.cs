using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Units listed by this frame's <see cref="PlayerCommand"/>s, each command owning the range its
    /// <see cref="PlayerCommand.SubjectStart"/> and <see cref="PlayerCommand.SubjectCount"/> name. Lets a command
    /// carry its own group, as a networked one does, instead of reading the player's selection.
    /// </summary>
    public struct PlayerCommandSubject : IBufferElementData
    {
        public Entity Value;
    }
}
