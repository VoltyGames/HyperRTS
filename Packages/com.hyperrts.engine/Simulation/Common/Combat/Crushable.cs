using Unity.Entities;

namespace HyperRTS.Simulation.Common.Combat
{
    /// <summary>A unit an enemy vehicle with a big enough <c>Crusher</c> kills by driving over it.</summary>
    public struct Crushable : IComponentData
    {
        /// <summary>Smallest <c>Crusher</c> size that crushes it; 0 = never.</summary>
        public int Crush;
    }
}
