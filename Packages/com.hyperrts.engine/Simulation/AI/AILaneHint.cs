using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// One attack path from the base of slot <see cref="From"/> to the base of slot <see cref="To"/>, as waypoints
    /// ~50 m apart (a range of <see cref="AIHintPoint"/>, neither base included). Every lane exists in both
    /// directions; a pair has one lane per distinct path.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AILaneHint : IBufferElementData
    {
        public byte From;
        public byte To;
        public ushort Start;
        public ushort Count;
    }
}
