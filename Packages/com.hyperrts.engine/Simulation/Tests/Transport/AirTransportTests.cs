using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Transport
{
    /// <summary>Boarding and leaving a container that flies.</summary>
    public class AirTransportTests : SimulationFixture
    {
        [Test]
        public void HoveringHelicopter_LandsToBeBoarded_AndToUnload()
        {
            var helicopter = World.SpawnAircraft(1, float3.zero);
            TransportSetup.AddContainer(World.Writer(helicopter), new Container { Capacity = 2, MaxPassengerSize = 2 });
            World.Run(2f);
            Assert.AreEqual(10f, World.GetPosition(helicopter).y, 1e-3f, "hovers at its altitude");

            var soldier = World.SpawnUnit(1, new float3(6f, 0f, 0f));
            TransportSetup.AddPassenger(World.Writer(soldier), 1);
            World.Enter(soldier, helicopter);
            var lowest = 10f;
            for (var t = 0f; t < 10f && !World.IsInside(soldier); t += TestWorld.FrameTime)
            {
                World.Tick();
                lowest = math.min(lowest, World.GetPosition(helicopter).y);
            }

            Assert.IsTrue(World.IsInside(soldier), "the soldier boards");
            Assert.Less(lowest, 0.5f, "only once the helicopter stood on the ground");
            World.Run(2f);
            Assert.AreEqual(10f, World.GetPosition(helicopter).y, 1e-3f, "which climbs back with no one waiting");

            World.Command(1, new PlayerCommand { Type = CommandType.Unload, Unit = helicopter, Argument = -1 });
            World.Tick();
            Assert.IsTrue(World.IsInside(soldier), "no one steps out in the air");
            Assert.IsTrue(World.RunUntil(() => !World.IsInside(soldier), 10f), "the soldier gets out");
            Assert.Less(World.GetPosition(helicopter).y, 0.5f, "once the helicopter stood on the ground");
            Assert.IsTrue(World.RunUntil(() => World.GetPosition(helicopter).y > 9.9f, 10f), "and it climbs back");
        }
    }
}
