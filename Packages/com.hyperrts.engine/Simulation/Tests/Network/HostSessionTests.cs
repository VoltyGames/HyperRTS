using HyperRTS.Network.Commands;
using HyperRTS.Network.Cues;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Network
{
    /// <summary>
    /// A single-world host with a remote client: both players are seated, the host's commands run once without an
    /// RPC, the client's arrive, and cues reach the client while the host keeps one copy.
    /// </summary>
    public class HostSessionTests
    {
        private HostTestKit _session;

        [SetUp]
        public void SetUp()
        {
            _session = new HostTestKit(1, 2);
            _session.Host.SpawnBuilding(1, float3.zero, new float2(4f, 4f));
            _session.Host.SpawnBuilding(2, new float3(40f, 0f, 40f), new float2(4f, 4f));
            Assert.IsTrue(_session.RunUntil(_session.Joined, 20f), "both players seated and the match started");
        }

        [TearDown]
        public void TearDown() => _session.Dispose();

        [Test]
        public void HostSystems_LeaveOutRemoteClientOnlySystems()
        {
            var host = _session.Host.World;
            Assert.AreEqual(SystemHandle.Null, host.GetExistingSystem<CommandSendSystem>(), "commands run in place");
            Assert.AreEqual(SystemHandle.Null, host.GetExistingSystem<CueReceiveSystem>(), "cues are already local");
            Assert.AreNotEqual(SystemHandle.Null, host.GetExistingSystem<ServerJoinSystem>());
            Assert.AreNotEqual(SystemHandle.Null, host.GetExistingSystem<ClientJoinSystem>());
        }

        [Test]
        public void HostAndClient_EachHoldTheirOwnSlot()
        {
            var hostPlayer = HostTestKit.LocalPlayer(_session.Host);
            var clientPlayer = HostTestKit.LocalPlayer(_session.Client);

            var factions = new[]
            {
                _session.Host.Get<Player>(hostPlayer).Faction, _session.Client.Get<Player>(clientPlayer).Faction,
                _session.Host.Singleton<LocalFaction>().Value, _session.Client.Singleton<LocalFaction>().Value,
            };
            CollectionAssert.AreEqual(new byte[] { 1, 2, 1, 2 }, factions, string.Join(",", factions));
        }

        [Test]
        public void HostCommand_RunsOnceInPlace()
        {
            var host = _session.Host;
            var prefab = host.UnitPrefab(host.CreateSupplies(), 0, buildTime: 30f);
            var producer = host.SpawnProducer(prefab);
            var player = HostTestKit.LocalPlayer(host);

            host.EntityManager.GetBuffer<PlayerCommand>(player)
                .Add(new PlayerCommand { Type = CommandType.Produce, Unit = producer, Prefab = prefab });
            _session.Tick(30);

            Assert.AreEqual(1, host.QueueLength(producer), $"queued {host.QueueLength(producer)}");
        }

        [Test]
        public void ClientCommand_ReachesItsPlayerOnTheHost()
        {
            var player = HostTestKit.LocalPlayer(_session.Client);
            _session.Client.EntityManager.GetBuffer<PlayerCommand>(player)
                .Add(new PlayerCommand { Type = CommandType.Surrender });

            Assert.IsTrue(_session.RunUntil(() => _session.Host.IsEnabled<Defeated>(_session.Host.Player(2)), 2f));
            Assert.IsFalse(_session.Host.IsEnabled<Defeated>(_session.Host.Player(1)));
        }

        [Test]
        public void Cues_ReachTheClient_AndStayOnceOnTheHost()
        {
            var host = _session.Host;
            var cue = host.Own(ScriptableObject.CreateInstance<SoundCue>());
            var prefab = host.MakePrefab(host.SpawnUnit(0, new float3(90f, 0f, 90f), name: "Tank"));
            host.SetBuildTime(prefab, 0.25f);
            var producer = host.MakeProducer(host.SpawnBuilding(2, new float3(40f, 0f, 30f), new float2(4f, 4f),
                name: "Factory"), new float3(0f, 0f, -4f), prefab);
            var writer = new EntityManagerWriter(host.EntityManager, producer);
            EntitySoundsAuthoring.Set(EntitySoundsAuthoring.Add(ref writer), CueSlot.ProductionDone, cue);
            host.SpawnProvider(2, new float3(30f, 0f, 40f), 10);

            host.Produce(2, producer, prefab);
            int onHost = 0, onClient = 0;
            for (var t = 0f; t < 1.5f; t += HostTestKit.FrameTime)
            {
                _session.Tick();
                onHost += host.Queued(CueSlot.ProductionDone).Length;
                onClient += _session.Client.Queued(CueSlot.ProductionDone).Length;
                Assert.AreEqual(0, ReceivedCueRpcs(host), "the host sends itself no cues");
            }

            Assert.AreEqual((1, 1), (onHost, onClient), $"host {onHost}, client {onClient}");
        }

        private static int ReceivedCueRpcs(TestWorld world)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(CueRpc), typeof(ReceiveRpcCommandRequest));
            return query.CalculateEntityCount();
        }
    }
}
