using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Population accounting and victory resolution.</summary>
    public class MatchTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp() => _world = new TestWorld();

        [TearDown]
        public void TearDown() => _world.Dispose();

        private MatchState Match()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(MatchState));
            return query.GetSingleton<MatchState>();
        }

        private void Kill(Entity entity) =>
            _world.EntityManager.SetComponentData(entity, new Health { Current = 0f, Max = 100f });

        [Test]
        public void Population_CountsLivingUnitsAndCompletedProviders()
        {
            _world.CreateMatch(1, 2);
            _world.SpawnUnit(1, float3.zero);
            _world.SpawnUnit(1, new float3(2f, 0f, 0f));
            _world.SpawnUnit(2, new float3(4f, 0f, 0f));
            _world.SpawnProvider(1, new float3(10f, 0f, 0f), 10);
            var unfinished = _world.SpawnProvider(1, new float3(20f, 0f, 0f), 5);
            _world.EntityManager.SetComponentEnabled<ConstructionProgress>(unfinished, true);

            _world.Tick();

            Assert.AreEqual(new Population { Used = 2, Cap = 10 }, _world.Get<Population>(_world.Player(1)));
            Assert.AreEqual(new Population { Used = 1, Cap = 0 }, _world.Get<Population>(_world.Player(2)));
        }

        [Test]
        public void Victory_NobodyLosesBeforeOwningCriticalEntities()
        {
            _world.CreateMatch(1, 2);
            _world.Tick(frames: 10);
            _world.SpawnBuilding(1, float3.zero, new float2(4f, 4f));
            _world.Tick(frames: 10);

            Assert.AreEqual(MatchPhase.Playing, Match().Phase);
            Assert.IsFalse(_world.IsEnabled<Defeated>(_world.Player(1)));
            Assert.IsFalse(_world.IsEnabled<Defeated>(_world.Player(2)), "player 2 hasn't streamed in yet");
        }

        [Test]
        public void Victory_LastTeamStandingWins()
        {
            _world.CreateMatch(1, 1, 2);
            _world.SpawnBuilding(1, float3.zero, new float2(4f, 4f));
            _world.SpawnUnit(2, new float3(10f, 0f, 0f));
            var enemy = _world.SpawnUnit(3, new float3(20f, 0f, 0f));
            _world.Tick();

            Kill(enemy);
            _world.Tick(frames: 2);

            Assert.IsTrue(_world.IsEnabled<Defeated>(_world.Player(3)));
            Assert.IsFalse(_world.IsEnabled<Defeated>(_world.Player(1)));
            Assert.AreEqual(new MatchState { Phase = MatchPhase.Ended, WinningTeam = 1, Contenders = 0b1110 }, Match());
        }

        [Test]
        public void Victory_NoSurvivorsIsADraw()
        {
            _world.CreateMatch(1, 2);
            var first = _world.SpawnUnit(1, float3.zero);
            var second = _world.SpawnUnit(2, new float3(10f, 0f, 0f));
            _world.Tick();

            Kill(first);
            Kill(second);
            _world.Tick(frames: 2);

            Assert.AreEqual(MatchPhase.Ended, Match().Phase);
            Assert.AreEqual(0, Match().WinningTeam);
        }
    }
}
