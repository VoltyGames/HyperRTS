using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// What a map tells the bot in one start slot (faction number): the map's resource fields by walking distance
    /// from its base, and where the lanes leading to it pass crossings or narrow ground. Ranges index
    /// <see cref="AIHintPoint"/>.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AISlotHint : IBufferElementData
    {
        public byte Slot;

        /// <summary>Resource node positions, nearest first, the slot's own main field first of all.</summary>
        public ushort ExpansionStart;

        public ushort ExpansionCount;

        /// <summary>Where to hold the ground in front of the base, nearest to it first.</summary>
        public ushort DefenceStart;

        public ushort DefenceCount;
    }
}
