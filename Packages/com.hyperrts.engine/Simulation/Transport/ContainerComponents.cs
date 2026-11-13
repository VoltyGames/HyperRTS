using HyperRTS.Simulation.Destruction;
using Unity.Entities;
using Unity.Mathematics;
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
        public bool HasExit;

        /// <summary>Where passengers step out, relative to the container, when it has an exit.</summary>
        public float3 Exit;

        /// <summary>Whether a passenger of <paramref name="size"/> fits, with <paramref name="lost"/> slots closed.</summary>
        public readonly bool Fits(DynamicBuffer<Cargo> cargo, int size, int lost) =>
            size <= MaxPassengerSize && Used(cargo) + size <= Capacity - lost;

        /// <summary>Slots on the floors that fell, closed until they are rebuilt.</summary>
        public readonly int LostSlots(in IntactFloors floors) =>
            math.min(Capacity, PerFloor(floors.Count) * math.countbits((uint)floors.Fallen));

        /// <summary>Slots each of <paramref name="floors"/> floors holds, rounded up.</summary>
        public readonly int PerFloor(int floors) => (Capacity + floors - 1) / math.max(floors, 1);

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

    /// <summary>
    /// Enabled while a passenger in reach waits to board a container in the air, so a hovering aircraft that stands
    /// lands for it. <see cref="BoardingSystem"/> calls afresh each frame and the aircraft clears the call as it moves.
    /// </summary>
    public struct BoardingCall : IComponentData, IEnableableComponent
    {
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
