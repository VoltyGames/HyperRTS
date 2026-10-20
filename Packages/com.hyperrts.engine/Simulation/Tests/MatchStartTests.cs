using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.AI;
using NUnit.Framework;
using Unity.Collections;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>When a networked match stops waiting for joins, and what the hold pauses meanwhile.</summary>
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

        [Test]
        public void JoinHold_PausesEveryPhaseButOrders_AndEveryAI()
        {
            using var world = new TestWorld();
            var orders = world.World.GetExistingSystemManaged<OrderSystemGroup>();
            var ai = world.World.GetExistingSystemManaged<AISystemGroup>();
            var combat = world.World.GetExistingSystemManaged<CombatSystemGroup>();
            CollectionAssert.Contains(ai.GetAllSystems().AsArray().ToArray(),
                world.World.Unmanaged.GetExistingUnmanagedSystem<SkirmishAISystem>(), "the skirmish AI runs in the AI group");

            var resume = GameplayPhases.PauseAllButOrders(world.World);
            Assert.IsTrue(orders.Enabled, "players still spawn and join");
            Assert.IsFalse(ai.Enabled, "no AI plays");
            Assert.IsFalse(combat.Enabled);

            GameplayPhases.Restore(world.World, resume);
            Assert.IsTrue(ai.Enabled);
            Assert.IsTrue(combat.Enabled);
        }

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
