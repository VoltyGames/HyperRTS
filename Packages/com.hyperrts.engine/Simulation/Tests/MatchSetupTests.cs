using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>A runtime <see cref="MatchSetup"/> reshapes the baked match before play, and players can surrender.</summary>
    public class MatchSetupTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp() => _world = new TestWorld();

        [TearDown]
        public void TearDown()
        {
            MatchSetupRequest.Clear();
            _world.Dispose();
        }

        private static SlotSetup Slot(byte team, PlayerControl control = PlayerControl.AI, byte side = 0) => new()
        {
            Open = true,
            Control = control,
            Team = team,
            Name = $"Slot {team}",
            Color = new float4(1f, 0f, 0f, 1f),
            Difficulty = AIDifficulty.Hard,
            Side = side,
        };

        private MatchSetup Setup(params SlotSetup[] slots)
        {
            var setup = MatchSetup.Create();
            foreach (var slot in slots)
            {
                setup.Slots.Add(slot);
            }

            return setup;
        }

        private int PlayerCount()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(Player));
            return query.CalculateEntityCount();
        }

        [Test]
        public void ClosedSlots_LosePlayerAndEverythingTheyOwn()
        {
            _world.CreateMatch(1, 2, 3, 4);
            var kept = _world.SpawnUnit(2, float3.zero);
            var closedUnit = _world.SpawnUnit(3, new float3(10f, 0f, 0f));
            var closedBase = _world.SpawnBuilding(4, new float3(30f, 0f, 0f), new float2(4f, 4f));
            MatchSetupRequest.Set(Setup(Slot(1, PlayerControl.LocalHuman), Slot(2)));

            _world.Tick(frames: 3);

            Assert.AreEqual(2, PlayerCount());
            Assert.IsTrue(_world.EntityManager.Exists(kept));
            Assert.IsFalse(_world.EntityManager.Exists(closedUnit));
            Assert.IsFalse(_world.EntityManager.Exists(closedBase));
            Assert.AreEqual(MatchPhase.Playing, MatchState().Phase, "closed slots are not defeated players");
        }

        [Test]
        public void OpenSlots_TakeNameColourTeamSideAndControl()
        {
            _world.CreateMatch(1, 2);
            MatchSetupRequest.Set(Setup(Slot(2, PlayerControl.AI, side: 3), Slot(2, PlayerControl.LocalHuman, side: 5)));

            _world.Tick();

            var first = _world.Player(1);
            var second = _world.Player(2);
            Assert.AreEqual("Slot 2", _world.Get<Player>(first).Name.ToString());
            Assert.AreEqual(new float4(1f, 0f, 0f, 1f), _world.Get<Player>(first).Color);
            Assert.AreEqual(3, _world.Get<PlayerSide>(first).Value);
            Assert.IsTrue(_world.EntityManager.HasComponent<AIPlayer>(first));
            Assert.IsFalse(_world.EntityManager.HasComponent<LocalPlayer>(first));
            Assert.IsTrue(_world.EntityManager.HasComponent<LocalPlayer>(second));
            Assert.IsFalse(_world.EntityManager.HasComponent<AIPlayer>(second));
            Assert.IsTrue(Relations().IsAllied(1, 2), "both slots were put on team 2");
        }

        [Test]
        public void AISlots_UseTheBakedTuningOfTheirDifficulty()
        {
            _world.CreateMatch(1, 2);
            var tunings = _world.EntityManager.GetBuffer<AIDifficultyTuning>(MatchEntity());
            tunings.Clear();
            tunings.Add(new AIDifficultyTuning
            {
                Difficulty = AIDifficulty.Brutal,
                Tuning = new AIPlayer { ThinkInterval = 0.25f, AttackWaveSize = 3 },
                IncomeMultiplier = 1.5f,
            });
            var brutal = Slot(2);
            brutal.Difficulty = AIDifficulty.Brutal;
            MatchSetupRequest.Set(Setup(Slot(1, PlayerControl.LocalHuman), brutal));

            _world.Tick();

            var ai = _world.Player(2);
            Assert.AreEqual(0.25f, _world.Get<AIPlayer>(ai).ThinkInterval);
            Assert.AreEqual(1.5f, _world.Get<IncomeMultiplier>(ai).Value);
        }

        [Test]
        public void StartingResourcesAndFog_FollowTheSetup()
        {
            _world.CreateMatch(1, 2);
            var supplies = ScriptableObject.CreateInstance<ResourceType>();
            ResourceMath.Add(_world.EntityManager.GetBuffer<ResourceStock>(_world.Player(1)), supplies, 1000);
            var setup = Setup(Slot(1, PlayerControl.LocalHuman), Slot(2));
            setup.StartingResourceScale = 2.5f;
            setup.Fog = FogOverride.Off;
            MatchSetupRequest.Set(setup);

            _world.Tick();

            var stock = _world.EntityManager.GetBuffer<ResourceStock>(_world.Player(1));
            Assert.AreEqual(2500, ResourceMath.GetAmount(stock, supplies));
            Assert.IsFalse(_world.Get<MapSettings>(MatchEntity()).FogOfWar);
            Object.DestroyImmediate(supplies);
        }

        [Test]
        public void Setup_AppliesOnlyOnce()
        {
            _world.CreateMatch(1, 2);
            MatchSetupRequest.Set(Setup(Slot(1, PlayerControl.LocalHuman), Slot(2)));
            _world.Tick();

            _world.EntityManager.SetComponentData(_world.Player(2), new Player { Faction = 2, Name = "Renamed" });
            _world.Tick();

            Assert.AreEqual("Renamed", _world.Get<Player>(_world.Player(2)).Name.ToString());
        }

        [Test]
        public void WithoutSetup_TheBakedMatchIsUntouched()
        {
            _world.CreateMatch(1, 2, 3);

            _world.Tick();

            Assert.AreEqual(3, PlayerCount());
            Assert.AreEqual("P3", _world.Get<Player>(_world.Player(3)).Name.ToString());
        }

        [Test]
        public void Surrender_DefeatsThePlayerAndEndsTheMatch()
        {
            _world.CreateMatch(1, 2);
            _world.SpawnBuilding(1, float3.zero, new float2(4f, 4f));
            _world.SpawnBuilding(2, new float3(40f, 0f, 0f), new float2(4f, 4f));
            _world.Tick();

            _world.Command(1, new PlayerCommand { Type = CommandType.Surrender });
            _world.Tick(frames: 2);

            Assert.IsTrue(_world.IsEnabled<Defeated>(_world.Player(1)));
            Assert.AreEqual(MatchPhase.Ended, MatchState().Phase);
            Assert.AreEqual(2, MatchState().WinningTeam);
        }

        private Entity MatchEntity()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(MapSettings));
            return query.GetSingletonEntity();
        }

        private MatchState MatchState()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(MatchState));
            return query.GetSingleton<MatchState>();
        }

        private FactionRelations Relations()
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(FactionRelations));
            return query.GetSingleton<FactionRelations>();
        }
    }
}
