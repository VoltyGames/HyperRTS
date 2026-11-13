using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Transport
{
    /// <summary>Boarding a container that flies.</summary>
    public class AirTransportTests : SimulationFixture
    {
        [Test]
        public void HoveringHelicopter_LandsForAWaitingPassenger_AndIsBoardedOnTheGround()
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
        }
    }
}
