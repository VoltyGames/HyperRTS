using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Squads;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Combat
{
    /// <summary>Hits on a sheltering building reach its garrison past the walls, by damage type and building health.</summary>
    public class GarrisonDamageTests : SimulationFixture
    {
        private const float Protection = 0.6f;
        private const float Shot = 20f;

        private DamageType _smallArms;
        private DamageType _highExplosive;

        [SetUp]
        public void Prepare()
        {
            _smallArms = Type(0.5f);
            _highExplosive = Type(1.3f);
        }

        private DamageType Type(float garrisonFactor)
        {
            var type = World.Own(ScriptableObject.CreateInstance<DamageType>());
            type.garrisonFactor = garrisonFactor;
            return type;
        }

        private Entity House(float3 position)
        {
            var house = World.SpawnBuilding(1, position, new float2(8f, 8f));
            var writer = World.Writer(house);
            TransportSetup.AddContainer(writer, new Container { Capacity = 5, MaxPassengerSize = 1 });
            TransportSetup.AddShelter(writer, Protection);
            return house;
        }

        private Entity Squad(float3 position)
        {
            var squad = World.SpawnUnit(1, position);
            var writer = World.Writer(squad);
            TransportSetup.AddPassenger(writer, 1);
            return squad;
        }

        /// <summary>Puts <paramref name="squad"/> straight into <paramref name="house"/>, as boarding ends.</summary>
        private void Garrison(Entity squad, Entity house)
        {
            World.Buffer<Cargo>(house).Add(new Cargo { Unit = squad, Size = 1 });
            World.Set(squad, new Inside { Container = house });
            World.SetEnabled<Inside>(squad, true);
        }

        private void Hit(Entity target, DamageType type, float amount = Shot) =>
            World.QueueDamage(new DamageEvent
            {
                Target = target,
                Origin = new float3(30f, 0f, 0f),
                SourceOwner = 2,
                Profile = new DamageProfile { Amount = amount, Type = type, GarrisonFactor = DamageType.Garrison(type) },
            });

        /// <summary>One frame resolves the hit on the house, the next its passengers' shares.</summary>
        private void Resolve() => World.Tick(frames: 3);

        private float Lost(Entity unit) => 100f - World.GetHealth(unit);

        [Test]
        public void GarrisonedSquad_LosesLessThanInTheOpen()
        {
            var house = House(float3.zero);
            var inside = Squad(new float3(10f, 0f, 0f));
            var outside = Squad(new float3(40f, 0f, 0f));
            Garrison(inside, house);
            World.Tick();

            Hit(house, _smallArms);
            Hit(outside, _smallArms);
            Resolve();

            Assert.AreEqual(Shot * (1f - Protection) * 0.5f, Lost(inside), 1e-3f);
            Assert.AreEqual(Shot, Lost(outside), 1e-3f);
        }

        [Test]
        public void ShareOnASquad_LandsOnOneMember()
        {
            var house = House(float3.zero);
            var squad = Squad(new float3(10f, 0f, 0f));
            SquadAuthoring.Add(World.Writer(squad), 100f, new byte[] { 0, 0, 0, 0 });
            Garrison(squad, house);
            World.Tick();

            Hit(house, _smallArms);
            Resolve();

            var members = World.Buffer<SquadMember>(squad);
            var hurt = 0;
            foreach (var member in members)
            {
                hurt += member.Health < member.MaxHealth ? 1 : 0;
            }

            Assert.AreEqual(1, hurt);
            Assert.AreEqual(SquadMember.Total(members), World.GetHealth(squad), 1e-3f);
            Assert.AreEqual(Shot * (1f - Protection) * 0.5f, Lost(squad), 1e-3f);
        }

        [Test]
        public void HighExplosive_HurtsTheGarrisonMoreThanSmallArms()
        {
            var shot = House(float3.zero);
            var shelled = House(new float3(30f, 0f, 0f));
            var rifles = Squad(new float3(10f, 0f, 0f));
            var shells = Squad(new float3(40f, 0f, 0f));
            Garrison(rifles, shot);
            Garrison(shells, shelled);
            World.Tick();

            Hit(shot, _smallArms);
            Hit(shelled, _highExplosive);
            Resolve();

            Assert.Greater(Lost(shells), Lost(rifles));
            Assert.AreEqual(Shot * (1f - Protection) * 1.3f, Lost(shells), 1e-3f);
        }

        [Test]
        public void DamagedBuilding_ProtectsLess()
        {
            var house = House(float3.zero);
            var squad = Squad(new float3(10f, 0f, 0f));
            Garrison(squad, house);
            var health = World.Get<Health>(house);
            health.Current = health.Max * 0.5f;
            World.Set(house, health);
            World.Tick();

            Hit(house, _smallArms);
            Resolve();

            Assert.AreEqual(Shot * (1f - Protection * 0.5f) * 0.5f, Lost(squad), 1e-3f);
        }

        [Test]
        public void Hit_IsSharedAmongThePassengers()
        {
            var house = House(float3.zero);
            var first = Squad(new float3(10f, 0f, 0f));
            var second = Squad(new float3(12f, 0f, 0f));
            Garrison(first, house);
            Garrison(second, house);
            World.Tick();

            Hit(house, _highExplosive);
            Resolve();

            var share = Shot * (1f - Protection) * 1.3f / 2f;
            Assert.AreEqual(share, Lost(first), 1e-3f);
            Assert.AreEqual(share, Lost(second), 1e-3f);
        }

        [Test]
        public void PassengerKilledInside_FreesItsSlot()
        {
            var house = House(float3.zero);
            var squad = Squad(new float3(10f, 0f, 0f));
            Garrison(squad, house);
            World.Tick();

            Hit(house, _highExplosive, 400f);
            Resolve();

            Assert.IsEmpty(World.Buffer<Cargo>(house).AsNativeArray().ToArray());
        }
    }
}
