using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HyperRTS.Network.Session;
using NUnit.Framework;
using Unity.Entities;
using Unity.Netcode;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Simulation.Tests.Network
{
    /// <summary>
    /// Session plumbing without opening sockets: relay drivers, connection status, rejoin guards, launch flags and
    /// the server report.
    /// </summary>
    public class SessionTests
    {
        // Session statics outlive Play mode in the editor; start each test as a fresh player would.
        [SetUp]
        public void ResetSession() => NetworkSession.ResetStatics();

        [TestCase(ConnectionState.State.Connecting, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Handshake, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Approval, NetworkStatus.Connecting)]
        [TestCase(ConnectionState.State.Connected, NetworkStatus.Connected)]
        [TestCase(ConnectionState.State.Disconnected, NetworkStatus.Disconnected)]
        public void ConnectionStateMapsToStatus(ConnectionState.State state, NetworkStatus expected) =>
            Assert.AreEqual(expected, NetworkStatusSystem.ToStatus(state));

        [TestCase(WorldFlags.GameServer | WorldFlags.GameClient, TestName = "RelayHostListensOnTheRelayOnly")]
        [TestCase(WorldFlags.GameClient, TestName = "RelayClientJoinsThroughTheRelay")]
        public void DriverStore_HasOneRelaySocket(WorldFlags flags)
        {
            var store = Build(flags, FakeRelay());
            Assert.AreEqual(1, store.DriversCount);
            Assert.AreEqual(TransportType.Socket, store.GetDriverType(store.FirstDriver));
            store.Dispose();
        }

        [Test]
        public void Token_IsNeverZero() => Assert.AreNotEqual(0UL, NetworkSession.Token);

        [Test]
        public void Rejoin_WithoutADirectJoin_ChangesNothing()
        {
            LogAssert.Expect(LogType.Error, new Regex("Nothing to rejoin"));
            Assert.IsFalse(NetworkSession.CanRejoin);
            Assert.IsFalse(NetworkSession.Rejoin(SessionScene.Keep));
        }

        private static LaunchOptions Parse(string line) => LaunchOptions.Parse(line.Split(' '));

        [Test]
        public void ObserverClient_ReadsAddressPortAndObserve()
        {
            var options = Parse("game.exe -batchmode -connect 127.0.0.1 -port 8000 -observe");

            Assert.AreEqual(LaunchMode.Client, options.Mode);
            Assert.AreEqual("127.0.0.1", options.Address);
            Assert.AreEqual(8000, options.Port);
            Assert.IsTrue(options.Observe);
        }

        [Test]
        public void Server_ReadsItsEndAndReport()
        {
            var options = Parse("game.exe -server -scene Maps/A.unity -exit-on-end -time-limit 45.5 -report out.json");

            Assert.AreEqual(LaunchMode.Server, options.Mode);
            Assert.AreEqual(NetworkSession.DefaultPort, options.Port);
            Assert.AreEqual("Maps/A.unity", options.ScenePath);
            Assert.IsTrue(options.ExitOnEnd);
            Assert.AreEqual(45.5f, options.TimeLimitMinutes);
            Assert.AreEqual("out.json", options.ReportPath);
            Assert.IsTrue(options.EndsItself);
        }

        [Test]
        public void NoSessionFlag_StartsNothing()
        {
            var options = Parse("game.exe -screen-fullscreen 0");

            Assert.AreEqual(LaunchMode.None, options.Mode);
            Assert.IsNull(options.ScenePath);
            Assert.IsFalse(options.EndsItself);
        }

        [TestCase("game.exe -host -observe", TestName = "ObserveNeedsConnect")]
        [TestCase("game.exe -connect 1.2.3.4 -exit-on-end", TestName = "ExitOnEndNeedsServer")]
        [TestCase("game.exe -server -report out.json", TestName = "ReportNeedsAnEnd")]
        [TestCase("game.exe -server -host", TestName = "OnlyOneMode")]
        [TestCase("game.exe -server -port 0", TestName = "PortZeroIsRejected")]
        [TestCase("game.exe -server -port lots", TestName = "PortMustBeANumber")]
        [TestCase("game.exe -server -time-limit -5", TestName = "TimeLimitMustBePositive")]
        [TestCase("game.exe -connect", TestName = "ConnectNeedsAnAddress")]
        public void BadFlags_Throw(string line) => Assert.Throws<ArgumentException>(() => Parse(line));

        [Test]
        public void Summarize_GivesMeanNearestRankP95AndMax_AndZeroForNothing()
        {
            var values = new List<float>();
            for (var i = 20; i >= 1; i--)
            {
                values.Add(i);
            }

            var stat = ServerReport.Stat.Summarize(values);

            Assert.AreEqual(10.5f, stat.mean, 1e-4f);
            Assert.AreEqual(19f, stat.p95);
            Assert.AreEqual(20f, stat.max);
            Assert.AreEqual(0f, ServerReport.Stat.Summarize(new List<float>()).max, "nothing summarizes to zero");
        }

        [Test]
        public void Json_CarriesResultPlayersAndCost()
        {
            var report = new ServerReport { outcome = ServerReport.Ended, winningTeam = 2, samples = 3 };
            report.players.Add(new ServerReport.PlayerResult { slot = 1, team = 1, unitsBuilt = 4 });
            report.sentKbps = ServerReport.Stat.Summarize(new List<float> { 1f, 2f, 3f });

            var json = JsonUtility.ToJson(report);
            foreach (var key in new[] { "outcome", "exitCode", "matchMinutes", "winningTeam", "players", "unitsBuilt",
                         "cpuPercentOfCore", "systemMemoryMB", "usedMemoryMB", "connections", "sentKbps", "entities",
                         "p95" })
            {
                StringAssert.Contains($"\"{key}\"", json);
            }

            var read = JsonUtility.FromJson<ServerReport>(json);
            Assert.AreEqual(2, read.winningTeam);
            Assert.AreEqual(4, read.players[0].unitsBuilt);
            Assert.AreEqual(3f, read.sentKbps.max);
        }

        private static NetworkDriverStore Build(WorldFlags flags, RelayServerData relay)
        {
            ClientServerBootstrap.AutoConnectPort = 0;
            using var world = flags == WorldFlags.GameClient
                ? ClientServerBootstrap.CreateClientWorld("Relay Test")
                : ClientServerBootstrap.CreateSingleWorldHost("Relay Test");
            var constructor = new RelayDriverConstructor(relay);
            var store = new NetworkDriverStore();
            var netDebug = new NetDebug { LogLevel = NetDebug.LogLevelType.Error };
            if (world.IsServer())
            {
                constructor.CreateServerDriver(world, ref store, netDebug);
            }
            else
            {
                constructor.CreateClientDriver(world, ref store, netDebug);
            }

            return store;
        }

        // Transport rejects an all-zero allocation id.
        private static RelayServerData FakeRelay()
        {
            var allocationId = new byte[16];
            allocationId[0] = 1;
            return new RelayServerData("127.0.0.1", 7777, allocationId, new byte[255], new byte[255], new byte[64], false);
        }
    }
}
