using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Difficulty presets from <see cref="MatchAuthoring"/> change how fast and how big the AI attacks.</summary>
    public class AIDifficultyTests
    {
        private TestWorld _world;
        private MatchAuthoring _match;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2, 3);
            _match = new GameObject("Match").AddComponent<MatchAuthoring>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_match.gameObject);
        }

        private Entity[] Squad(byte faction, AIDifficulty difficulty, float3 at)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, _world.Player(faction));
            AIPlayerSetup.Add(ref writer, _match.AITuningFor(difficulty).ToComponent());

            var squad = new Entity[5];
            for (var i = 0; i < squad.Length; i++)
            {
                squad[i] = _world.AddWeapon(_world.SpawnUnit(faction, at + new float3(i * 2f, 0f, 0f)));
            }

            return squad;
        }

        [Test]
        public void HarderAI_ThinksSooner_AndAttacksWithSmallerWaves()
        {
            var easy = _match.AITuningFor(AIDifficulty.Easy).ToComponent();
            var hard = _match.AITuningFor(AIDifficulty.Hard).ToComponent();
            Assert.Greater(easy.ThinkInterval, hard.ThinkInterval);
            Assert.Greater(easy.AttackWaveSize, hard.AttackWaveSize);
            Assert.IsFalse(easy.UseAbilities);

            var easySquad = Squad(2, AIDifficulty.Easy, new float3(-10f, 0f, 40f));
            var hardSquad = Squad(3, AIDifficulty.Hard, new float3(-10f, 0f, -40f));
            _world.SpawnBuilding(1, new float3(80f, 0f, 0f), new float2(4f, 4f));

            _world.Run(5f);

            Assert.AreEqual(OrderType.AttackMove, _world.Get<ActiveOrder>(hardSquad[0]).Value.Type, "a wave of five is enough");
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(easySquad[0]), "easy waits for a bigger wave");
        }
    }
}
