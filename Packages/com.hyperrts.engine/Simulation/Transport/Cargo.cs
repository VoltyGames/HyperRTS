using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>A passenger aboard, in boarding order.</summary>
    [InternalBufferCapacity(0)]
    public struct Cargo : IBufferElementData
    {
        public Entity Unit;
        public int Size;
    }
}
