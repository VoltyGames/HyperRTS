using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>A passenger aboard, in boarding order.</summary>
    [InternalBufferCapacity(0)]
    public struct Cargo : IBufferElementData
    {
        [GhostField] public Entity Unit;
        [GhostField] public int Size;
    }
}
