using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Part of the map anyone can wreck (fences, trees, houses, bridges): splash from every side hits it, explicit
    /// attack orders may target it, and fog never hides it.
    /// </summary>
    public struct Destructible : IComponentData
    {
        /// <summary>Smallest <c>Crusher</c> size that flattens it by driving over it; 0 = never.</summary>
        public int Crush;
    }
}
