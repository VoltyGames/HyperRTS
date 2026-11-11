using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>A hit <see cref="SquadTestKit.HitSquad(TestWorld, Unity.Entities.Entity, TestHit)"/> queues on a squad.</summary>
    public sealed class TestHit
    {
        public float Amount;
        public float3 At;
        public float Radius;
        public float Edge = 1f;
        public float Suppression;
        public float Piercing;
        /// <summary>Metres past <see cref="At"/> the shooter stands.</summary>
        public float3 From;
    }
}
