using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Holds passengers listed in its <see cref="Cargo"/> buffer.</summary>
    public struct Container : IComponentData
    {
        public int Capacity;
        public int MaxPassengerSize;
        public bool PassengersFire;
        public bool PassengersSurvive;

        public readonly bool Fits(DynamicBuffer<Cargo> cargo, int size)
        {
            var used = 0;
            foreach (var item in cargo)
            {
                used += item.Size;
            }

            return size <= MaxPassengerSize && used + size <= Capacity;
        }
    }
}
