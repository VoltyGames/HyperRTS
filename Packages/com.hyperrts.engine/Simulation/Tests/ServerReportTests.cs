using System.Collections.Generic;
using HyperRTS.Network.Session;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The server report's statistics and the JSON keys tools read from it.</summary>
    public class ServerReportTests
    {
        [Test]
        public void Summarize_GivesMeanNearestRankP95AndMax()
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
        }

        [Test]
        public void Summarize_OfNothing_IsZero()
        {
            var stat = ServerReport.Stat.Summarize(new List<float>());

            Assert.AreEqual(0f, stat.max);
        }

        [Test]
        public void Json_CarriesResultPlayersAndCost()
        {
            var report = new ServerReport { outcome = ServerReport.Ended, winningTeam = 2, samples = 3 };
            report.players.Add(new ServerReport.PlayerResult { slot = 1, team = 1, unitsBuilt = 4 });
            report.sentKbps = ServerReport.Stat.Summarize(new List<float> { 1f, 2f, 3f });

            var json = JsonUtility.ToJson(report);
            foreach (var key in new[] { "outcome", "exitCode", "matchMinutes", "winningTeam", "players", "unitsBuilt",
                         "cpuPercentOfCore", "workingSetMB", "usedMemoryMB", "connections", "sentKbps", "entities",
                         "p95" })
            {
                StringAssert.Contains($"\"{key}\"", json);
            }

            var read = JsonUtility.FromJson<ServerReport>(json);
            Assert.AreEqual(2, read.winningTeam);
            Assert.AreEqual(4, read.players[0].unitsBuilt);
            Assert.AreEqual(3f, read.sentKbps.max);
        }
    }
}
