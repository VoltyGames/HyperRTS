using HyperRTS.Simulation.Combat;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Stance restored when the passenger gets out; inside, passengers hold position or stay passive.</summary>
    public struct PassengerStance : IComponentData
    {
        public Stance Value;
    }
}
