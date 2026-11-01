using HyperRTS.Simulation.Auras;
using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Auras
{
    /// <summary>Area auras: who they affect, presence, bonuses, healing, damage, no stacking.</summary>
    public class AuraTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity SpawnAura(byte faction, float3 position, string name, AuraTargets affects, float heal = 0f,
            float damage = 0f, params StatModifier[] bonuses)
        {
            var source = _world.SpawnBuilding(faction, position, new float2(2f, 2f));
            var id = EntityInfo.TypeIdFromName(name);
            for (var i = 0; i < bonuses.Length; i++)
            {
                bonuses[i].Source = StatSource.Aura(id);
            }

            var writer = new EntityManagerWriter(_world.EntityManager, source);
            AuraAuthoring.AddAura(ref writer, new Aura
            {
                AuraId = id, Radius = 10f, Affects = affects, HealPerSecond = heal, DamagePerSecond = damage,
            }, bonuses);
            return source;
        }

        [Test]
        public void FriendlyAura_HealsAndBoosts_OnlyAllies()
        {
            var speed = new StatModifier { Stat = Stat.MoveSpeed, Percent = 0.5f };
            SpawnAura(1, float3.zero, "Repair Zone", AuraTargets.Friendly | AuraTargets.Units, heal: 20f, bonuses: speed);
            var friend = _world.SpawnUnit(1, new float3(3f, 0f, 0f), speed: 0f);
            _world.EntityManager.SetComponentData(friend, new MovementSpeed { Value = 4f });
            _world.EntityManager.SetComponentData(friend, new Health { Current = 50f, Max = 100f });
            var enemy = _world.SpawnUnit(2, new float3(-3f, 0f, 0f), speed: 0f);
            _world.EntityManager.SetComponentData(enemy, new Health { Current = 50f, Max = 100f });

            _world.Run(1.05f);

            Assert.AreEqual(70f, _world.Get<Health>(friend).Current, 1f);
            Assert.AreEqual(6f, _world.Get<MovementSpeed>(friend).Value, 1e-3f);
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<AuraPresence>(friend).Length);
            Assert.AreEqual(50f, _world.Get<Health>(enemy).Current, "enemies are excluded");
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<AuraPresence>(enemy).Length);
        }

        [Test]
        public void HostileAura_DamagesEnemies_AndCreditsSource()
        {
            var source = SpawnAura(1, float3.zero, "Radiation", AuraTargets.Enemies | AuraTargets.AllKinds, damage: 40f);
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f), speed: 0f);

            _world.Run(1.05f);

            Assert.AreEqual(60f, _world.Get<Health>(enemy).Current, 1f);
            Assert.AreEqual(source, _world.Get<LastAttacker>(enemy).Source);
        }

        [Test]
        public void SameNamedAuras_DoNotStack()
        {
            SpawnAura(1, new float3(-2f, 0f, 0f), "Radiation", AuraTargets.Enemies | AuraTargets.Units, damage: 40f);
            SpawnAura(1, new float3(2f, 0f, 0f), "Radiation", AuraTargets.Enemies | AuraTargets.Units, damage: 40f);
            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 3f), speed: 0f);

            _world.Run(1.05f);

            Assert.AreEqual(60f, _world.Get<Health>(enemy).Current, 1f);
        }

        [TestCase(false, TestName = "LeavingAura_RemovesBonus")]
        [TestCase(true, TestName = "DestroyedLastAura_RemovesBonus")]
        public void EndingAura_RemovesBonus(bool destroySource)
        {
            var armor = new StatModifier { Stat = Stat.DamageTaken, Percent = -0.5f };
            var source = SpawnAura(1, float3.zero, "Shield", AuraTargets.Friendly | AuraTargets.Units, bonuses: armor);
            var unit = _world.SpawnUnit(1, new float3(3f, 0f, 0f), speed: 0f);
            _world.Run(0.5f);
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<StatModifier>(unit).Length);

            if (destroySource)
            {
                _world.EntityManager.DestroyEntity(source);
            }
            else
            {
                _world.EntityManager.SetComponentData(unit, LocalTransform.FromPosition(new float3(50f, 0f, 0f)));
            }

            _world.Run(0.5f);

            Assert.AreEqual(0, _world.EntityManager.GetBuffer<StatModifier>(unit).Length);
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<AuraPresence>(unit).Length);
        }

        [Test]
        public void LeavingAura_KeepsOtherSourcesWithTheSameId()
        {
            var armor = new StatModifier { Stat = Stat.DamageTaken, Percent = -0.5f };
            SpawnAura(1, float3.zero, "Shield", AuraTargets.Friendly | AuraTargets.Units, bonuses: armor);
            var unit = _world.SpawnUnit(1, new float3(3f, 0f, 0f), speed: 0f);
            var upgrade = StatSource.Upgrade(EntityInfo.TypeIdFromName("Shield"));
            _world.EntityManager.GetBuffer<StatModifier>(unit)
                .Add(new StatModifier { Stat = Stat.MaxHealth, Add = 50f, Source = upgrade });
            _world.Run(0.5f);

            _world.EntityManager.SetComponentData(unit, LocalTransform.FromPosition(new float3(50f, 0f, 0f)));
            _world.Run(0.5f);

            var modifiers = _world.EntityManager.GetBuffer<StatModifier>(unit);
            Assert.AreEqual(1, modifiers.Length, "an upgrade sharing the aura's id survives");
            Assert.AreEqual(upgrade, modifiers[0].Source);
            Assert.AreEqual(150f, _world.Get<Health>(unit).Max, 1e-3f);
        }

        [Test]
        public void Auras_AffectUnitsAndBuildings_ButNotScenery()
        {
            var relations = default(FactionRelations);
            var everything = AuraTargets.Own | AuraTargets.AllKinds;
            Assert.IsTrue(AuraRules.Affects(everything, 1, new SpatialEntry { Faction = 1, IsUnit = true }, relations));
            Assert.IsTrue(AuraRules.Affects(everything, 1, new SpatialEntry { Faction = 1, IsBuilding = true },
                relations));
            Assert.IsFalse(AuraRules.Affects(everything, 1, new SpatialEntry { Faction = 1 }, relations), "scenery");
        }
    }
}
