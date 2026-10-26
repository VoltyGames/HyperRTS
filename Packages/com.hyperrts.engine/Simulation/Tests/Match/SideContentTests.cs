using System.Text.RegularExpressions;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Replays;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Simulation.Tests.Match
{
    /// <summary>A player's side picks its start kit, spawned at its spawn, and its AI build order.</summary>
    public class SideContentTests
    {
        private const float Ground = 2f;
        private static readonly float3[] Spawns = { new(-40f, 0f, 0f), new(40f, 0f, 0f), new(0f, 0f, 40f) };
        private static readonly float[] Yaws = { 0f, math.PI / 2f, math.PI };

        private TestWorld _world;
        private Entity _base;
        private Entity _truck;

        [SetUp]
        public void SetUp() => _world = new TestWorld();

        [TearDown]
        public void TearDown()
        {
            MatchSetupRequest.Clear();
            _world.Dispose();
        }

        /// <summary>A three-player match, as a map bakes it: side 1's kit is a base and a truck, side 2 has none.</summary>
        private static void CreateSides(TestWorld world, out Entity basePrefab, out Entity truck)
        {
            world.CreateMatch(1, 2, 3);
            world.CreateTerrain(_ => Ground);
            basePrefab = world.MakePrefab(world.SpawnBuilding(0, float3.zero, new float2(4f, 4f), name: "Base"));
            truck = world.MakePrefab(world.SpawnUnit(0, float3.zero, name: "Truck"));
            var match = world.MatchEntity();
            var entityManager = world.EntityManager;
            var sides = entityManager.AddBuffer<MatchSide>(match);
            sides.Add(new MatchSide { Value = 1 });
            sides.Add(new MatchSide { Value = 2 });
            var kit = entityManager.AddBuffer<SideStartItem>(match);
            kit.Add(new SideStartItem { Side = 1, Prefab = basePrefab });
            kit.Add(new SideStartItem { Side = 1, Prefab = truck, Offset = new float2(2f, 5f), Yaw = math.PI / 2f });
            entityManager.AddBuffer<SideBuildStep>(match)
                .Add(new SideBuildStep { Side = 1, Prefab = basePrefab, Count = 2 });
            for (byte faction = 1; faction <= 3; faction++)
            {
                var player = world.Player(faction);
                entityManager.AddComponentData(player, new PlayerSpawn
                {
                    Position = Spawns[faction - 1], Yaw = Yaws[faction - 1],
                });
                entityManager.SetComponentData(player, new PlayerSide { Value = 1 });
                entityManager.AddBuffer<AIBuildStep>(player).Add(new AIBuildStep { Prefab = truck, Count = 9 });
            }
        }

        private static SlotSetup Slot(byte team, byte side) => new()
        {
            Open = true, Control = PlayerControl.AI, Team = team, Name = $"Slot {team}", Side = side,
        };

        private static void RequestSetup(params SlotSetup[] slots)
        {
            var setup = MatchSetup.Create();
            foreach (var slot in slots)
            {
                setup.Slots.Add(slot);
            }

            MatchSetupRequest.Set(setup);
        }

        private static int CountType(TestWorld world, Entity prefab)
        {
            var count = 0;
            for (byte faction = 0; faction <= 3; faction++)
            {
                count += world.Owned(prefab, faction).Count;
            }

            return count;
        }

        [Test]
        public void Kit_SpawnsForOpenSlots_AtTheSpawnTurnedByItsYaw_OnTheGround()
        {
            CreateSides(_world, out _base, out _truck);
            RequestSetup(Slot(1, 1), Slot(2, 1));

            _world.Tick();

            Assert.AreEqual(1, _world.Owned(_base, 1).Count);
            Assert.AreEqual(1, _world.Owned(_truck, 2).Count);
            Assert.AreEqual(0, _world.Owned(_base, 3).Count + _world.Owned(_truck, 3).Count, "slot 3 is closed");

            var homeBase = _world.Get<LocalTransform>(_world.Owned(_base, 1)[0]);
            Assert.That(math.distance(homeBase.Position, new float3(-40f, Ground, 0f)), Is.LessThan(1e-3f));

            // Spawn 2 faces +X: right is -Z, so the offset (2, 5) lands 2 m towards -Z and 5 m towards +X.
            var truck = _world.Get<LocalTransform>(_world.Owned(_truck, 2)[0]);
            Assert.That(math.distance(truck.Position, new float3(45f, Ground, -2f)), Is.LessThan(1e-3f));
            var facing = math.mul(truck.Rotation, new float3(0f, 0f, 1f));
            Assert.That(math.distance(facing, new float3(0f, 0f, -1f)), Is.LessThan(1e-3f), "spawn yaw plus item yaw");
        }

        [Test]
        public void CameraFacingItem_IgnoresTheSpawnsYaw_AndFacesMinusZ()
        {
            CreateSides(_world, out _base, out _truck);
            _world.EntityManager.GetBuffer<SideStartItem>(_world.MatchEntity()).Add(new SideStartItem
            {
                Side = 1, Prefab = _truck, Offset = new float2(2f, 5f), FacesCamera = true,
            });
            RequestSetup(Slot(1, 1), Slot(2, 1));

            _world.Tick();

            // Facing -Z, right is -X: the offset (2, 5) lands 2 m towards -X and 5 m towards -Z of spawn 2.
            var trucks = _world.Owned(_truck, 2);
            Assert.AreEqual(2, trucks.Count);
            var placed = false;
            foreach (var truck in trucks)
            {
                var pose = _world.Get<LocalTransform>(truck);
                if (math.distance(pose.Position, new float3(38f, Ground, -5f)) < 1e-3f)
                {
                    placed = true;
                    var facing = math.mul(pose.Rotation, new float3(0f, 0f, 1f));
                    Assert.That(math.distance(facing, new float3(0f, 0f, -1f)), Is.LessThan(1e-3f));
                }
            }

            Assert.IsTrue(placed, "a truck in the camera's frame");
        }

        [Test]
        public void Kit_SpawnsOnce_WhenTheSceneIsPlayedAsBaked()
        {
            CreateSides(_world, out _base, out _truck);

            _world.Tick(frames: 3);

            Assert.AreEqual(3, CountType(_world, _base));
            Assert.AreEqual(1, _world.Owned(_truck, 3).Count);
        }

        [Test]
        public void TheSide_PicksTheAIBuildOrder()
        {
            CreateSides(_world, out _base, out _truck);
            RequestSetup(Slot(1, 1), Slot(2, 2));

            _world.Tick();

            var first = _world.EntityManager.GetBuffer<AIBuildStep>(_world.Player(1));
            Assert.AreEqual(1, first.Length);
            Assert.AreEqual(_base, first[0].Prefab);
            Assert.AreEqual(2, first[0].Count);
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<AIBuildStep>(_world.Player(2)).Length,
                "side 2 has no build order, so its bot trains freely");
            Assert.AreEqual(0, _world.Owned(_base, 2).Count, "side 2 has no kit");
        }

        [Test]
        public void AnUnlistedSide_Throws_AndSpawnsNothing()
        {
            CreateSides(_world, out _base, out _truck);
            RequestSetup(Slot(1, 1), Slot(2, 5));

            LogAssert.Expect(LogType.Exception, new Regex("Player 2 plays side 5"));
            _world.Tick();

            Assert.AreEqual(0, CountType(_world, _base));
        }

        [Test]
        public void SideZero_Throws()
        {
            CreateSides(_world, out _base, out _truck);
            RequestSetup(Slot(1, 0), Slot(2, 1));

            LogAssert.Expect(LogType.Exception, new Regex("Player 1 plays side 0"));
            _world.Tick();
        }

        [Test]
        public void AReplay_ClaimsTheSpawnedKit_WithoutDoublingIt()
        {
            var replay = RecordKitMatch();
            CreateSides(_world, out _base, out _truck);
            MatchSetupRequest.Set(ReplaySetup.ToMatchSetup(replay));
            _world.Tick();
            ReplayViewer.Begin(_world.World, replay);

            _world.Tick(frames: 3);

            Assert.AreEqual(2, CountType(_world, _base));
            Assert.AreEqual(2, CountType(_world, _truck));
        }

        [Test]
        public void AReplay_StartedBeforeTheKitSpawns_SpawnsItFromThePrefabs()
        {
            var replay = RecordKitMatch();
            CreateSides(_world, out _base, out _truck);
            MatchSetupRequest.Set(ReplaySetup.ToMatchSetup(replay));
            ReplayViewer.Begin(_world.World, replay);

            _world.Tick(frames: 3);

            Assert.AreEqual(2, CountType(_world, _base));
            Assert.AreEqual(2, CountType(_world, _truck));
        }

        private static Replay RecordKitMatch()
        {
            using var world = new TestWorld();
            CreateSides(world, out _, out _);
            RequestSetup(Slot(1, 1), Slot(2, 1));
            ReplayRecorder.Start(world.World, sampleRate: 10f, keyframeInterval: 0.5f);
            world.Tick(frames: 10);
            MatchSetupRequest.Clear();
            return ReplayRecorder.Stop(world.World, "Assets/Maps/Test.unity");
        }
    }
}
