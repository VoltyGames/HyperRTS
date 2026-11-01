using HyperRTS.Network.Commands;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Network.Tests
{
    /// <summary>A client's selection reaches the server in pieces, and a command after it addresses all of it.</summary>
    public class SelectionRpcTests
    {
        private const int Army = 500;

        [Test]
        public void LargeSelection_SplitsIntoAReplaceThenAppends()
        {
            using var units = new NativeArray<Entity>(Army, Allocator.Temp);
            using var rpcs = new NativeList<SelectionRpc>(Allocator.Temp);

            SelectionRpc.Fill(units, 9, rpcs);

            Assert.AreEqual(4, rpcs.Length, "500 units in RPCs of 127");
            Assert.IsFalse(rpcs[0].Append, "the first replaces the server's selection");
            Assert.IsTrue(rpcs[1].Append && rpcs[3].Append);
            Assert.AreEqual(Army - 3 * SelectionRpc.Capacity, rpcs[3].Units.Length);
            Assert.IsTrue(rpcs[3].Precedes(9) && !rpcs[3].Precedes(8), "a command stamped 9 follows selection 9");
            Assert.IsTrue(new SelectionRpc { Version = ushort.MaxValue }.Precedes(1), "versions wrap");
        }

        [Test]
        public void EmptySelection_StillClearsTheServers()
        {
            using var rpcs = new NativeList<SelectionRpc>(Allocator.Temp);
            SelectionRpc.Fill(new NativeArray<Entity>(0, Allocator.Temp), 1, rpcs);
            Assert.AreEqual(1, rpcs.Length);
            Assert.IsFalse(rpcs[0].Append);
        }

        [TestCase(0, TestName = "ClientMovesFiveHundredUnits")]
        [TestCase(10, TestName = "ClientMovesFiveHundredUnits_ThroughPacketLoss")]
        public void ClientCommand_AddressesItsWholeSelection(int packetLoss)
        {
            using var session = HostTestKit.WithPacketLoss(packetLoss, 1, 2);
            var host = session.Host;
            host.SpawnBuilding(1, float3.zero, new float2(4f, 4f));
            host.SpawnBuilding(2, new float3(40f, 0f, 40f), new float2(4f, 4f));
            var prefab = session.GhostUnitPrefab(2, "Rifleman");
            Spawn(host, prefab);
            Assert.IsTrue(session.RunUntil(session.Joined, 20f), "both players seated and the match started");
            Assert.IsTrue(session.RunUntil(() => Ghosts(session.Client).Length == Army, 20f),
                $"the client sees {Ghosts(session.Client).Length} of the {Army} units");

            var client = session.Client;
            foreach (var unit in Ghosts(client))
            {
                client.SetEnabled<Selected>(unit, true);
            }

            client.EntityManager.GetBuffer<PlayerCommand>(HostTestKit.LocalPlayer(client))
                .Add(new PlayerCommand { Type = CommandType.Move, Position = new float3(-60f, 0f, 60f) });

            Assert.IsTrue(session.RunUntil(() => Moving(host) == Army, 5f), $"{Moving(host)} of {Army} units move");
        }

        private static void Spawn(TestWorld world, Entity prefab)
        {
            for (var i = 0; i < Army; i++)
            {
                // 25 by 20 on the 200 m map, clear of both bases.
                var position = new float3(20f + i % 25 * 2f, 0f, -60f + i / 25 * 2f);
                world.EntityManager.Spawn(prefab, position, quaternion.identity, 2);
            }
        }

        private static NativeArray<Entity> Ghosts(TestWorld world)
        {
            using var query = world.Query(typeof(GhostInstance), typeof(Selectable));
            return query.ToEntityArray(Allocator.Temp);
        }

        private static int Moving(TestWorld world)
        {
            var moving = 0;
            foreach (var unit in Ghosts(world))
            {
                if (world.IsEnabled<ActiveOrder>(unit) && world.Get<ActiveOrder>(unit).Value.Type == OrderType.Move)
                {
                    moving++;
                }
            }

            return moving;
        }
    }
}
