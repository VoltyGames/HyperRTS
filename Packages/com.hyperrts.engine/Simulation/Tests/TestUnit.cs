namespace HyperRTS.Simulation.Tests
{
    /// <summary>What <see cref="SpawnTestKit.SpawnUnit(TestWorld, byte, Unity.Mathematics.float3, TestUnit)"/> sets beyond its owner and position.</summary>
    public sealed class TestUnit
    {
        public float Speed = 5f;
        public float Radius = 0.5f;
        public float MaxHealth = 100f;
        public string Name = "Unit";
    }
}
