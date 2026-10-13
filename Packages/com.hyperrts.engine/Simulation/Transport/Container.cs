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

        public readonly bool Fits(DynamicBuffer<Cargo> cargo, int size) =>
            size <= MaxPassengerSize && Used(cargo) + size <= Capacity;

        /// <summary>Capacity the passengers in <paramref name="cargo"/> take up.</summary>
        public static int Used(DynamicBuffer<Cargo> cargo)
        {
            var used = 0;
            foreach (var item in cargo)
            {
                used += item.Size;
            }

            return used;
        }
    }
}
