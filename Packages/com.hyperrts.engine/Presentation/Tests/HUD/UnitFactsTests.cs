using HyperRTS.Presentation.HUD.Panels;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.HUD
{
    /// <summary>What the selection panel's details block reads from an entity's components.</summary>
    public class UnitFactsTests : HUDFixture
    {
        [Test]
        public void Read_ShowsTypeDataFromThePrefab_AndNothingWithoutOne()
        {
            var facts = new UnitFacts();
            var bare = _em.CreateEntity();
            facts.Read(_em, bare, Entity.Null);
            Assert.IsEmpty(facts.Weapons);
            Assert.IsEmpty(facts.Facts);

            var tank = _em.CreateEntity(typeof(Cargo));
            var prefab = _em.CreateEntity(typeof(WeaponMount), typeof(ArmorModifier), typeof(MovementSpeed),
                typeof(Container));
            var gun = new Weapon { Range = 30f, Targets = WeaponTargets.Ground | WeaponTargets.Air };
            _em.GetBuffer<WeaponMount>(prefab).Add(WeaponMount.Create(gun, default));
            var bullets = ScriptableObject.CreateInstance<DamageType>();
            _em.GetBuffer<ArmorModifier>(prefab).Add(new ArmorModifier { DamageType = bullets, Multiplier = 0.25f });
            _em.GetBuffer<ArmorModifier>(prefab).Add(new ArmorModifier { DamageType = bullets, Multiplier = 1f });
            _em.SetComponentData(prefab, new MovementSpeed { Value = 8f });
            _em.SetComponentData(prefab, new Container { Capacity = 5 });
            _em.GetBuffer<Cargo>(tank).Add(new Cargo { Size = 2 });

            facts.Read(_em, tank, prefab);

            Assert.AreEqual("Ground, Air, 30 m", facts.Weapons[0].Value);
            Assert.AreEqual(new[] { "Armor", "Speed", "Passengers" }, facts.Facts.ConvertAll(row => row.Name));
            Assert.AreEqual("Bullet 25%", facts.Facts[0].Value, "a multiplier of 1 is not listed");
            Assert.AreEqual("2 / 5", facts.Facts[2].Value);
            Object.DestroyImmediate(bullets);
        }
    }
}
