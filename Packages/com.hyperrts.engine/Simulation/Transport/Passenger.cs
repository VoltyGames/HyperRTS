using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>A unit that can enter containers.</summary>
    public struct Passenger : IComponentData
    {
        public int Size;
    }
}
