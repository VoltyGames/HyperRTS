using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// An entity changed hands this frame (capture, a game's mind control). Its <see cref="Faction"/> switches to
    /// <see cref="To"/> when the frame's commands play back; write it with <see cref="Ownership.Transfer"/>.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct OwnerChanged : IBufferElementData
    {
        public Entity Entity;
        public byte From;
        public byte To;
    }
}
