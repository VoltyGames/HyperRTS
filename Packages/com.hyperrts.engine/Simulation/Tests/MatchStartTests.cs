using HyperRTS.Network.Players;
using NUnit.Framework;
using Unity.Collections;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>When a networked match stops waiting for joins.</summary>
    public class MatchStartTests
    {
        [Test]
        public void NoPlayersYet_IsNotJoined() => Assert.IsFalse(MatchStartSystem.AllJoined(0, Humans()));

        [Test]
        public void AllAIMatch_IsJoinedOncePlayersSpawn() => Assert.IsTrue(MatchStartSystem.AllJoined(2, Humans()));

        [Test]
        public void HumanWithoutConnection_IsNotJoined() =>
            Assert.IsFalse(MatchStartSystem.AllJoined(3, Humans(4, 0)));

        [Test]
        public void EveryHumanConnected_IsJoined() => Assert.IsTrue(MatchStartSystem.AllJoined(3, Humans(4, 5)));

        private static NativeArray<PlayerConnection> Humans(params int[] networkIds)
        {
            var humans = new NativeArray<PlayerConnection>(networkIds.Length, Allocator.Temp);
            for (var i = 0; i < networkIds.Length; i++)
            {
                humans[i] = new PlayerConnection { NetworkId = networkIds[i] };
            }

            return humans;
        }
    }
}
