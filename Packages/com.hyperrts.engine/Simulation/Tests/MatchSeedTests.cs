using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Match;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The match seed gives each bot a stable, distinct attack lane seed.</summary>
    public class MatchSeedTests
    {
        [TearDown]
        public void TearDown() => MatchSetupRequest.Clear();

        private static SlotSetup BotSlot(byte team) => new()
        {
            Open = true,
            Control = PlayerControl.AI,
            Team = team,
            Name = "Bot",
            Color = new float4(1f, 0f, 0f, 1f),
            Difficulty = AIDifficulty.Hard,
        };

        private static (uint First, uint Second) RouteSeeds(uint seed)
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 2);
            var setup = MatchSetup.Create();
            setup.Seed = seed;
            setup.Slots.Add(BotSlot(1));
            setup.Slots.Add(BotSlot(2));
            MatchSetupRequest.Set(setup);

            world.Tick();

            return (world.Get<AIPlayer>(world.Player(1)).RouteSeed, world.Get<AIPlayer>(world.Player(2)).RouteSeed);
        }

        [Test]
        public void BotsOfDifferentFactions_GetDifferentNonzeroRouteSeeds_TheSameOnEveryRun()
        {
            var first = RouteSeeds(12345u);
            var again = RouteSeeds(12345u);

            Assert.AreNotEqual(0u, first.First);
            Assert.AreNotEqual(0u, first.Second);
            Assert.AreNotEqual(first.First, first.Second);
            Assert.AreEqual(first, again);
        }

        [Test]
        public void DifferentMatchSeeds_GiveABotDifferentRouteSeeds()
        {
            Assert.AreNotEqual(RouteSeeds(1u).First, RouteSeeds(2u).First);
        }

        [Test]
        public void ZeroMatchSeed_LeavesRouteSeedsUnset()
        {
            Assert.AreEqual((0u, 0u), RouteSeeds(0u));
        }
    }
}
