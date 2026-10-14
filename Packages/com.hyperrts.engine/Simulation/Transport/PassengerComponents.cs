using HyperRTS.Simulation.Combat;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>A unit that can enter containers.</summary>
    public struct Passenger : IComponentData
    {
        public int Size;
    }

    /// <summary>Stance restored when the passenger gets out; inside, passengers hold position or stay passive.</summary>
    public struct PassengerStance : IComponentData
    {
        public Stance Value;
    }
}
