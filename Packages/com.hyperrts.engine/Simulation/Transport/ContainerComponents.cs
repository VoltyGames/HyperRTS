using Unity.Entities;
using Unity.Netcode;

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

    /// <summary>A passenger aboard, in boarding order.</summary>
    [InternalBufferCapacity(0)]
    public struct Cargo : IBufferElementData
    {
        [GhostField] public Entity Unit;
        [GhostField] public int Size;
    }

    /// <summary>A neutral building anyone may garrison: it belongs to its passengers while occupied, neutral when empty.</summary>
    public struct NeutralGarrison : IComponentData
    {
    }
}
