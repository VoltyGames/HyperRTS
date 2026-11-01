using HyperRTS.Network.Commands;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Network.Tests
{
    /// <summary>Player commands survive the wire.</summary>
    public class CommandRpcTests
    {
        [Test]
        public void FreeYaw_RoundTripsThroughTheCommandRpc()
        {
            var command = new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Position = new float3(3f, 0f, 7f), Yaw = 0.7f, Queue = true,
            };

            var received = CommandRpc.From(command).ToCommand();

            Assert.AreEqual(CommandType.PlaceBuilding, received.Type);
            Assert.AreEqual(0.7f, received.Yaw);
            Assert.AreEqual(command.Position, received.Position);
            Assert.IsTrue(received.Queue);
        }
    }
}
