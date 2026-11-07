using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Units listed by this frame's <see cref="PlayerCommand"/>s, each command owning the range its
    /// <see cref="PlayerCommand.SubjectStart"/> and <see cref="PlayerCommand.SubjectCount"/> name, so a networked
    /// command carries its own group instead of the player's selection.
    /// </summary>
    public struct PlayerCommandSubject : IBufferElementData
    {
        public Entity Value;
    }
}
