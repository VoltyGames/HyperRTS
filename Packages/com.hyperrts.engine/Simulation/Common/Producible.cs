using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Production data read from a prefab: seconds to build and population used.</summary>
    public struct Producible : IComponentData
    {
        public float BuildTime;
        public int Population;
    }
}
