using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>What <see cref="BuildingSetup"/> adds to a building entity.</summary>
    public struct BuildingSpec
    {
        public float2 Footprint;
        public int PopulationProvided;
        public bool Complete;
        public float Power;
        public bool RememberedInFog;
        public bool OpenFootprint;
    }
}
