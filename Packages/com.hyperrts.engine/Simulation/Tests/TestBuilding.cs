namespace HyperRTS.Simulation.Tests
{
    /// <summary>What <see cref="SpawnTestKit.SpawnBuilding(TestWorld, byte, Unity.Mathematics.float3, Unity.Mathematics.float2, TestBuilding)"/> sets beyond its footprint.</summary>
    public sealed class TestBuilding
    {
        public bool Complete = true;
        public float BuildTime = 10f;
        public string Name = "Building";
        public int PopulationProvided;
        public float Power;
    }
}
