using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// The floors of a building that still stand, a bit each, ground floor first. Combat marks the ones that fell;
    /// a container gives each floor an even share of its slots and empties a floor once it falls.
    /// </summary>
    public struct IntactFloors : IComponentData
    {
        public byte Count;

        /// <summary>Floors that came down and are not rebuilt yet.</summary>
        public byte Fallen;

        /// <summary>Fallen floors whose passengers are already out, kept by the container.</summary>
        public byte Emptied;

        public readonly int Standing => Count - math.countbits((uint)Fallen);
    }
}
