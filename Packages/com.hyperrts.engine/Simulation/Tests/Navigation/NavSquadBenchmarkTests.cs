using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Navigation
{
    /// <summary>Benchmarks of squads moving.</summary>
    [Category("Benchmark")]
    public class NavSquadBenchmarkTests : NavBenchmarkFixture
    {
        // 2,700 squads of four marching from the two end strips to each other's.
        [Test, Performance]
        public void SquadMemberFrame_TwentySevenHundredSquads()
        {
            var squads = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                for (var i = 0; i < 1350; i++)
                {
                    var position = new float3(side * (-295f + i % 16 * 4f), 0f, -290f + i / 16 * 4f);
                    var squad = World.SpawnSquad(1, position);
                    World.Send(squad, new float3(-position.x, 0f, position.z), side < 0f ? 1 : 2);
                    squads++;
                }
            }

            World.Tick();
            Assert.AreEqual(2700, squads, "every squad exists");
            World.Run(10f);
            var median = BenchmarkKit.Median("SquadMemberFrame2700", World.UpdateSystem<SquadMemberSystem>,
                () => World.Tick());

            UnityEngine.Debug.Log($"[scale] 2700-squad member frame: median {median:F3} ms");
        }
    }
}
