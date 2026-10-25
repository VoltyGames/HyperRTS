using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The skirmish AI on a map's hints: attack lanes chosen by seed, and an error when the hints are missing.</summary>
    public class AIHintsTests
    {
        private static readonly Vector2[] North = { new(-10f, -40f), new(30f, -40f) };
        private static readonly Vector2[] South = { new(-10f, 40f), new(30f, 40f) };
        private static readonly Vector2[] None = Array.Empty<Vector2>();

        /// <summary>Slots 1 and 2 with the given lanes from 2 to 1 (and the same ones back).</summary>
        private static AIMapHintsData Lanes(params Vector2[][] lanes)
        {
            var data = new AIMapHintsData();
            data.AddSlot(1, None, None);
            data.AddSlot(2, None, None);
            foreach (var lane in lanes)
            {
                data.AddLane(2, 1, lane);
                data.AddLane(1, 2, lane.Reverse().ToArray());
            }

            return data;
        }

        /// <summary>
        /// Bot 2 with a wave of two soldiers at <paramref name="wave"/> and the enemy base at (60, 0, 0) attacks once;
        /// the wave's orders come back: the centre of its formation slots for the current one, then each queued one.
        /// </summary>
        private static List<float3> AttackOrders(uint seed, AIMapHintsData hints, float3 wave)
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 2);
            var writer = new EntityManagerWriter(world.EntityManager, world.Player(2));
            AIPlayerSetup.Add(ref writer, new AIPlayer { ThinkInterval = 0.5f, AttackWaveSize = 2, RouteSeed = seed });
            world.CreateHints(hints);
            var first = world.AddWeapon(world.SpawnUnit(2, wave));
            var second = world.AddWeapon(world.SpawnUnit(2, wave + new float3(0f, 0f, 4f)));
            world.SpawnBuilding(1, new float3(60f, 0f, 0f), new float2(4f, 4f));

            world.Tick(frames: 3);

            return Legs(world, first).Zip(Legs(world, second), (a, b) => (a + b) * 0.5f).ToList();
        }

        private static IEnumerable<float3> Legs(TestWorld world, Entity soldier) =>
            world.EntityManager.GetBuffer<QueuedOrder>(soldier).AsNativeArray().Select(queued => queued.Value.Position)
                .Prepend(world.Get<ActiveOrder>(soldier).Value.Position).ToList();

        private static List<float3> AttackOrders(uint seed, AIMapHintsData hints) =>
            AttackOrders(seed, hints, new float3(-40f, 0f, 0f));

        [Test]
        public void PickLane_StaysInRange_AndUsesEveryLaneAcrossAttacks()
        {
            var used = new HashSet<int>();
            for (var attack = 0; attack < 64; attack++)
            {
                var pick = AIRoutes.PickLane(3u, 2, 1, attack, 3);
                Assert.That(pick, Is.InRange(0, 2));
                used.Add(pick);
            }

            Assert.AreEqual(3, used.Count, "a bot varies its route from one attack to the next");
        }

        [Test]
        public void PickLane_DiffersBetweenSeedsAndBots()
        {
            var bySeed = Enumerable.Range(0, 16).Select(seed => AIRoutes.PickLane((uint)seed, 2, 1, 0, 4)).Distinct();
            var byBot = Enumerable.Range(2, 16).Select(bot => AIRoutes.PickLane(0u, (byte)bot, 1, 0, 4)).Distinct();
            Assert.Greater(bySeed.Count(), 1);
            Assert.Greater(byBot.Count(), 1);
        }

        [Test]
        public void Attack_FollowsALane_ThroughItsWaypointsToTheEnemyBase()
        {
            var orders = AttackOrders(0u, Lanes(North, South));

            Assert.AreEqual(3, orders.Count, "two waypoints, then the base");
            Assert.AreEqual(40f, math.abs(orders[0].z), 0.01f);
            Assert.AreEqual(-10f, orders[0].x, 0.01f);
            Assert.AreEqual(30f, orders[1].x, 0.01f);
            Assert.AreEqual(math.sign(orders[0].z), math.sign(orders[1].z), "both waypoints are on the same lane");
            Assert.Less(math.distance(orders[2], new float3(60f, 0f, 0f)), 5f);
        }

        [Test]
        public void Attack_TakesTheSameLane_ForTheSameSeed()
        {
            var hints = Lanes(North, South);

            foreach (var seed in new[] { 0u, 1u, 2u, 3u })
            {
                CollectionAssert.AreEqual(AttackOrders(seed, hints), AttackOrders(seed, hints), $"seed {seed}");
            }
        }

        [Test]
        public void Attack_VariesItsLane_WithTheSeed()
        {
            var hints = Lanes(North, South);

            var lanes = Enumerable.Range(0, 12).Select(seed => math.sign(AttackOrders((uint)seed, hints)[0].z))
                .Distinct().ToList();

            CollectionAssert.AreEquivalent(new[] { -1f, 1f }, lanes);
        }

        [Test]
        public void Attack_StartsAtTheWaypointNearestTheWave()
        {
            var orders = AttackOrders(0u, Lanes(North), new float3(32f, 0f, -36f));

            Assert.AreEqual(2, orders.Count, "the waypoint the wave already passed is skipped");
            Assert.AreEqual(30f, orders[0].x, 0.01f);
        }

        [Test]
        public void Attack_OnlyTargetsEnemiesTheMapHasLanesTo()
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 2, 3);
            var writer = new EntityManagerWriter(world.EntityManager, world.Player(2));
            AIPlayerSetup.Add(ref writer, new AIPlayer { ThinkInterval = 0.5f, AttackWaveSize = 1 });
            var data = new AIMapHintsData();
            foreach (var slot in new[] { 1, 2, 3 })
            {
                data.AddSlot(slot, None, None);
            }

            data.AddLane(2, 3, None);
            data.AddLane(3, 2, None);
            world.CreateHints(data);
            var soldier = world.AddWeapon(world.SpawnUnit(2, float3.zero));
            world.SpawnBuilding(1, new float3(30f, 0f, 0f), new float2(4f, 4f));
            world.SpawnBuilding(3, new float3(80f, 0f, 0f), new float2(4f, 4f));

            world.Tick(frames: 3);

            var goal = world.Get<ActiveOrder>(soldier).Value.Position;
            Assert.Less(math.distance(goal, new float3(80f, 0f, 0f)), 5f, "the nearer enemy has no lane from this slot");
        }

        [Test]
        public void MapWithoutHints_LogsOneErrorNamingTheMap_AndTheAIStaysIdle()
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 2);
            var writer = new EntityManagerWriter(world.EntityManager, world.Player(2));
            AIPlayerSetup.Add(ref writer, new AIPlayer { ThinkInterval = 0.5f, AttackWaveSize = 2 });
            world.RemoveHints();
            var supplies = world.CreateSupplies();
            var harvester = world.MakeHarvester(world.SpawnUnit(2, float3.zero), 10, 1f);
            world.SpawnNode(new float3(5f, 0f, 0f), supplies, 100);
            var map = Regex.Escape(SceneManager.GetActiveScene().name);
            LogAssert.Expect(LogType.Error, new Regex($"Map '{map}' has no AI map hints"));

            world.Tick(frames: 5);

            Assert.IsFalse(world.IsEnabled<ActiveOrder>(harvester), "no hints, no AI");
        }

        [Test]
        public void HintsWithoutTheBotsSlot_LogAnErrorNamingTheSlot()
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 2);
            var writer = new EntityManagerWriter(world.EntityManager, world.Player(2));
            AIPlayerSetup.Add(ref writer, new AIPlayer { ThinkInterval = 0.5f, AttackWaveSize = 2 });
            var data = new AIMapHintsData();
            data.AddSlot(1, None, None);
            world.CreateHints(data);
            LogAssert.Expect(LogType.Error, new Regex("no AI map hints for slot 2"));

            world.Tick(frames: 3);
        }

        [Test]
        public void View_ReadsSlotsAndLanesInBakedOrder()
        {
            using var world = new TestWorld();
            world.CreateMatch(1, 2);
            var entity = world.CreateHints(Lanes(North, South));
            var manager = world.EntityManager;
            var view = new AIMapHintsView(manager.GetBuffer<AISlotHint>(entity), manager.GetBuffer<AILaneHint>(entity),
                manager.GetBuffer<AIHintPoint>(entity));

            Assert.AreEqual(2, view.LaneCount(2, 1));
            Assert.AreEqual(0, view.LaneCount(1, 1));
            Assert.AreEqual(Faction.Bit(1), view.LaneEnemies(2));
            var south = view.Lane(2, 1, 1);
            Assert.AreEqual(2, south.Count);
            Assert.AreEqual(new float2(-10f, 40f), view.Waypoint(south, 0));
            Assert.AreEqual(1, view.NearestWaypoint(south, new float2(50f, 40f)));
            Assert.IsTrue(view.TryGetSlot(2, out _));
            Assert.IsFalse(view.TryGetSlot(3, out _));
        }

        [Test]
        public void Data_RejectsSlotsOutOfRange_RepeatedSlotsAndLanesToThemselves()
        {
            var data = new AIMapHintsData();
            data.AddSlot(1, None, None);

            Assert.Throws<ArgumentException>(() => data.AddSlot(1, None, None));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.AddSlot(0, None, None));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.AddLane(1, Faction.Max + 1, None));
            Assert.Throws<ArgumentException>(() => data.AddLane(1, 1, None));
        }
    }
}
