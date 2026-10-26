using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Fog of war per team, explored memory, the fog-off switch, and stealth with its detectors.</summary>
    public class VisionTests
    {
        private const byte Team1 = 1;
        private const byte Team2 = 2;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(Team1, Team2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Fog_RevealsVisionRange_OnlyForOwnersTeam()
        {
            _world.SpawnUnit(1, float3.zero);

            _world.Tick();
            var fog = _world.Fog();

            Assert.IsTrue(fog.IsVisible(new float3(5f, 0f, 0f), Team1));
            Assert.IsFalse(fog.IsVisible(new float3(5f, 0f, 0f), Team2), "enemies don't share vision");
            Assert.IsFalse(fog.IsVisible(new float3(30f, 0f, 0f), Team1), "beyond the vision range");
        }

        [Test]
        public void Fog_StampsTheHighestTeam()
        {
            const byte lastTeam = FactionRelations.MaxTeams - 1;
            using var world = new TestWorld();
            world.CreateMatch(lastTeam, Team1);
            world.SpawnUnit(1, float3.zero);

            world.Tick();
            var fog = world.Fog();

            Assert.IsTrue(fog.IsVisible(new float3(5f, 0f, 0f), lastTeam), "an eight-player FFA needs teams past 7");
            Assert.IsFalse(fog.IsVisible(new float3(5f, 0f, 0f), Team1));
        }

        [Test]
        public void SetTeam_RejectsTeamsPastTheFogBits_AndSlotsPastTheLastFaction()
        {
            var relations = new FactionRelations();

            Assert.Throws<System.ArgumentException>(() => relations.SetTeam(1, FactionRelations.MaxTeams));
            Assert.Throws<System.ArgumentException>(() => relations.SetTeam(1, -1));
            Assert.Throws<System.ArgumentException>(() => relations.SetTeam(Faction.Max + 1, 1));
        }

        [Test]
        public void Fog_KeepsExploredCells_AfterTheUnitLeaves()
        {
            var scout = _world.SpawnUnit(1, float3.zero);
            _world.Tick();
            var version = _world.Fog().Version;

            _world.EntityManager.SetComponentData(scout, LocalTransform.FromPosition(new float3(50f, 0f, 0f)));
            _world.Run(0.5f);
            var fog = _world.Fog();

            Assert.IsFalse(fog.IsVisible(float3.zero, Team1));
            Assert.IsTrue(fog.IsExplored(float3.zero, Team1));
            Assert.IsTrue(fog.IsVisible(new float3(50f, 0f, 0f), Team1));
            Assert.Greater(fog.Version, version, "restamps bump the version");
        }

        [Test]
        public void FogDisabled_EverythingVisible()
        {
            _world.DisableFog();

            _world.Tick();
            var fog = _world.Fog();

            Assert.IsTrue(fog.IsVisible(new float3(90f, 0f, -90f), Team1));
            Assert.IsTrue(fog.IsVisible(new float3(-90f, 0f, 90f), Team2));
            Assert.IsTrue(fog.IsExplored(float3.zero, Team2));
        }

        [Test]
        public void FogHidden_TagsOnlyUnseenEnemies_AndDeselectsThem()
        {
            var scout = _world.SpawnUnit(1, float3.zero);
            var seen = _world.SpawnUnit(2, new float3(5f, 0f, 0f));
            var unseen = _world.SpawnUnit(2, new float3(50f, 0f, 0f));
            _world.EntityManager.SetComponentEnabled<Selected>(unseen, true);

            _world.Tick(frames: 2);

            Assert.IsTrue(_world.EntityManager.HasComponent<FogHidden>(unseen));
            Assert.IsFalse(_world.EntityManager.HasComponent<FogHidden>(seen));
            Assert.IsFalse(_world.EntityManager.HasComponent<FogHidden>(scout), "own units are never hidden");
            Assert.IsFalse(_world.IsEnabled<Selected>(unseen), "enemies in fog drop out of the selection");
        }

        [Test]
        public void FogHidden_ClearedWhenFogIsDisabled()
        {
            var unseen = _world.SpawnUnit(2, new float3(50f, 0f, 0f));
            _world.Tick();
            Assert.IsTrue(_world.EntityManager.HasComponent<FogHidden>(unseen));

            _world.DisableFog();
            _world.Tick();

            Assert.IsFalse(_world.EntityManager.HasComponent<FogHidden>(unseen));
        }

        [Test]
        public void Detector_RevealsStealthedEnemy_OnlyInRange()
        {
            var soldier = Detect(_world.AddWeapon(_world.SpawnUnit(1, float3.zero)), radius: 5f);
            var enemy = Stealthy(_world.SpawnUnit(2, new float3(8f, 0f, 0f)));

            _world.Run(1f);
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier), "inside vision, outside detection");
            Assert.IsTrue(IsHidden(enemy));

            _world.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(new float3(3f, 0f, 0f)));
            _world.Run(0.3f);
            Assert.IsFalse(IsHidden(enemy));

            _world.Run(3f);
            Assert.IsFalse(_world.EntityManager.Exists(enemy), "detected, so acquired and killed");
        }

        [Test]
        public void Detector_OnLowPower_StopsDetecting_UntilPowered()
        {
            Detect(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f), power: -3f), radius: 10f);
            var enemy = Stealthy(_world.SpawnUnit(2, new float3(5f, 0f, 0f)));

            _world.Run(0.3f);
            Assert.IsTrue(IsHidden(enemy), "a radar on low power is off");

            _world.SpawnBuilding(1, new float3(-20f, 0f, 0f), new float2(4f, 4f), power: 5f);
            _world.Run(0.3f);
            Assert.IsFalse(IsHidden(enemy), "powered again, so it detects");
        }

        [Test]
        public void Firing_RevealsShooter_ThenItRestealths()
        {
            var shooter = Stealthy(_world.AddWeapon(_world.SpawnUnit(2, float3.zero), cooldown: 100f), reveal: 1f);
            var target = _world.SpawnUnit(1, new float3(3f, 0f, 0f), maxHealth: 1000f);

            _world.Run(0.4f);
            Assert.Less(_world.GetHealth(target), 1000f, "fired once");
            Assert.IsFalse(_world.IsEnabled<Stealthed>(shooter));
            Assert.IsFalse(IsHidden(shooter), "visible while revealed");

            _world.Run(1.2f);
            Assert.IsTrue(_world.IsEnabled<Stealthed>(shooter));
            Assert.IsTrue(IsHidden(shooter), "hidden again once the reveal ends");
        }

        [Test]
        public void FogOff_StillHidesStealthedEnemies_UntilDetected()
        {
            _world.DisableFog();
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            var enemy = Stealthy(_world.SpawnUnit(2, new float3(3f, 0f, 0f)));
            var far = _world.SpawnUnit(2, new float3(80f, 0f, 0f));

            _world.Run(1f);
            Assert.AreEqual(100f, _world.GetHealth(enemy));
            Assert.IsTrue(IsHidden(enemy));
            Assert.IsFalse(IsHidden(far), "fog off shows everything else");

            Detect(soldier, 5f);
            _world.Run(1f);
            Assert.IsFalse(IsHidden(enemy));
            Assert.Less(_world.GetHealth(enemy), 100f);
        }

        [Test]
        public void StealthedTarget_CannotBeOrderedAttacked()
        {
            var soldier = _world.AddWeapon(_world.SpawnUnit(1, float3.zero));
            var enemy = Stealthy(_world.SpawnUnit(2, new float3(15f, 0f, 0f)));
            _world.Tick();

            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.Smart, Unit = soldier, Target = enemy, Position = new float3(15f, 0f, 0f),
            });
            _world.Tick();

            Assert.AreEqual(OrderType.Move, _world.Get<ActiveOrder>(soldier).Value.Type);
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(soldier));
        }

        [Test]
        public void StealthDisabledOrMoving_LeavesUnitVisible()
        {
            var toggled = Stealthy(_world.SpawnUnit(2, new float3(3f, 0f, 0f)), enabled: false);
            var still = Stealthy(_world.SpawnUnit(2, new float3(-3f, 0f, 0f)), onlyWhenStill: true);
            _world.Tick();
            Assert.IsFalse(_world.IsEnabled<Stealthed>(toggled));
            Assert.IsTrue(_world.IsEnabled<Stealthed>(still));

            _world.EntityManager.SetComponentEnabled<Stealth>(toggled, true);
            _world.EntityManager.SetComponentData(still, new MoveDestination { Value = new float3(-40f, 0f, 0f) });
            _world.EntityManager.SetComponentEnabled<MoveDestination>(still, true);
            _world.Tick();

            Assert.IsTrue(_world.IsEnabled<Stealthed>(toggled), "games toggle stealth at runtime");
            Assert.IsFalse(_world.IsEnabled<Stealthed>(still), "still-only stealth drops while moving");
        }

        private Entity Stealthy(Entity entity, float reveal = 1f, bool onlyWhenStill = false, bool enabled = true)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            StealthSetup.AddStealth(ref writer,
                new Stealth { RevealDuration = reveal, OnlyWhenStill = onlyWhenStill }, enabled);
            return entity;
        }

        private Entity Detect(Entity entity, float radius)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            StealthSetup.AddDetector(ref writer, radius);
            return entity;
        }

        private bool IsHidden(Entity entity) => _world.EntityManager.HasComponent<FogHidden>(entity);
    }
}
