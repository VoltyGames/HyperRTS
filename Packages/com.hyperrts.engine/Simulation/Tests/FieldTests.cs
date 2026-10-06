using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Fields;
using HyperRTS.Simulation.Stats;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Area fields: presence, bonuses, healing, damage, no stacking.</summary>
    public class FieldTests
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

        private Entity Field(byte faction, float3 position, string name, FieldTargets affects, float heal = 0f,
            float damage = 0f, params StatModifier[] bonuses)
        {
            var source = _world.SpawnBuilding(faction, position, new float2(2f, 2f));
            var id = EntityInfo.TypeIdFromName(name);
            for (var i = 0; i < bonuses.Length; i++)
            {
                bonuses[i].Source = id;
            }

            var sink = new EntityManagerSink(_world.EntityManager, source);
            FieldSetup.AddField(ref sink, new AreaField
            {
                FieldId = id, Radius = 10f, Affects = affects, HealPerSecond = heal, DamagePerSecond = damage,
            }, bonuses);
            return source;
        }

        [Test]
        public void FriendlyField_HealsAndBoosts_OnlyAllies()
        {
            var speed = new StatModifier { Stat = Stat.MoveSpeed, Percent = 0.5f };
            Field(1, float3.zero, "Repair Zone", FieldTargets.Friendly | FieldTargets.Units, heal: 20f, bonuses: speed);
            var friend = _world.SpawnUnit(1, new float3(3f, 0f, 0f), speed: 0f);
            _world.EntityManager.SetComponentData(friend, new MovementSpeed { Value = 4f });
            _world.EntityManager.SetComponentData(friend, new Health { Current = 50f, Max = 100f });
            var enemy = _world.SpawnUnit(2, new float3(-3f, 0f, 0f), speed: 0f);
            _world.EntityManager.SetComponentData(enemy, new Health { Current = 50f, Max = 100f });

            _world.Run(1.05f);

            Assert.AreEqual(70f, _world.Get<Health>(friend).Current, 1f);
            Assert.AreEqual(6f, _world.Get<MovementSpeed>(friend).Value, 1e-3f);
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<FieldPresence>(friend).Length);
            Assert.AreEqual(50f, _world.Get<Health>(enemy).Current, "enemies are excluded");
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<FieldPresence>(enemy).Length);
        }

        [Test]
        public void HostileField_DamagesEnemies_AndCreditsSource()
        {
            var source = Field(1, float3.zero, "Radiation", FieldTargets.Enemies | FieldTargets.AllKinds, damage: 40f);
            var enemy = _world.SpawnUnit(2, new float3(4f, 0f, 0f), speed: 0f);

            _world.Run(1.05f);

            Assert.AreEqual(60f, _world.Get<Health>(enemy).Current, 1f);
            Assert.AreEqual(source, _world.Get<LastAttacker>(enemy).Source);
        }

        [Test]
        public void SameNamedFields_DoNotStack()
        {
            Field(1, new float3(-2f, 0f, 0f), "Radiation", FieldTargets.Enemies | FieldTargets.Units, damage: 40f);
            Field(1, new float3(2f, 0f, 0f), "Radiation", FieldTargets.Enemies | FieldTargets.Units, damage: 40f);
            var enemy = _world.SpawnUnit(2, new float3(0f, 0f, 3f), speed: 0f);

            _world.Run(1.05f);

            Assert.AreEqual(60f, _world.Get<Health>(enemy).Current, 1f);
        }

        [Test]
        public void LeavingField_RemovesBonus()
        {
            var armor = new StatModifier { Stat = Stat.DamageTaken, Percent = -0.5f };
            Field(1, float3.zero, "Shield", FieldTargets.Friendly | FieldTargets.Units, bonuses: armor);
            var unit = _world.SpawnUnit(1, new float3(3f, 0f, 0f), speed: 0f);
            _world.Run(0.5f);
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<StatModifier>(unit).Length);

            _world.EntityManager.SetComponentData(unit, LocalTransform.FromPosition(new float3(50f, 0f, 0f)));
            _world.Run(0.5f);

            Assert.AreEqual(0, _world.EntityManager.GetBuffer<StatModifier>(unit).Length);
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<FieldPresence>(unit).Length);
        }
    }
}
