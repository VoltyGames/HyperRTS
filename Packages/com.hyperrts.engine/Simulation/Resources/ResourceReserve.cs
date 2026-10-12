using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// Finite stock a <see cref="ResourceIncome"/> pays out of. At zero it pays nothing but stays, so a
    /// <see cref="ReserveRefill"/> can top it up again.
    /// </summary>
    public struct ResourceReserve : IComponentData
    {
        public int Remaining;
        public int Capacity;
    }
}
