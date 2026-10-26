using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Economy
{
    /// <summary>
    /// Airfield pads, the reload cycle (out of ammo or mount rounds, return, dock, reload) and losing or changing
    /// airfields.
    /// </summary>
    public class AirfieldTests
    {
        private static readonly float3 Pad = new(5f, 0f, 0f);

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void AircraftOutOfAmmo_ReturnsToItsPad_ReloadsAndIsReadyAgain()
        {
            var airfield = _world.SpawnAirfield(1, float3.zero, Pad);
            var aircraft = _world.AddWeapon(_world.SpawnAircraft(1, Pad), range: 4f, damage: 10f);
            _world.AddPadUser(aircraft, rounds: 2);
            _world.EntityManager.SetComponentData(aircraft, new HomePad { Airfield = airfield, Pad = 0 });
            var tank = _world.SpawnUnit(2, new float3(30f, 0f, 0f));

            Attack(aircraft, tank);
            Assert.IsTrue(_world.RunUntil(() => _world.Get<Ammo>(aircraft).Current == 0, 6f), "fires every round");
            Assert.IsTrue(_world.RunUntil(() => _world.IsEnabled<Docked>(aircraft), 8f), "flies home and docks");

            Assert.AreEqual(80f, _world.GetHealth(tank), "two rounds, then it held fire");
            Assert.Less(math.distance(_world.GetPosition(aircraft).xz, Pad.xz), 0.6f, "back on its pad");
            Assert.AreEqual(aircraft, _world.EntityManager.GetBuffer<LandingPad>(airfield)[0].Aircraft);

            _world.Run(1.5f);
            Assert.AreEqual(0f, _world.GetPosition(aircraft).y, 1e-3f, "landed");
            Assert.AreEqual(2, _world.Get<Ammo>(aircraft).Current, "reloaded while docked");

            Attack(aircraft, tank);
            _world.Run(4f);
            Assert.IsFalse(_world.IsEnabled<Docked>(aircraft), "took off again");
            Assert.AreEqual(60f, _world.GetHealth(tank), "and fired its fresh rounds");
        }

        [Test]
        public void SpentAircraft_IsSentHomeOnce_ThenObeysPlayerOrders()
        {
            var airfield = _world.SpawnAirfield(1, float3.zero, Pad);
            var aircraft = _world.AddWeapon(_world.SpawnAircraft(1, Pad), range: 4f, damage: 10f);
            _world.AddPadUser(aircraft, rounds: 1);
            _world.EntityManager.SetComponentData(aircraft, new HomePad { Airfield = airfield, Pad = 0 });
            var tank = _world.SpawnUnit(2, new float3(30f, 0f, 0f));

            Attack(aircraft, tank);
            Assert.IsTrue(_world.RunUntil(() => _world.Get<Ammo>(aircraft).Current == 0, 6f), "fires its round");
            _world.Tick();
            Assert.AreEqual(OrderType.ReturnToBase, _world.Get<ActiveOrder>(aircraft).Value.Type, "sent home");

            var away = new float3(-40f, 0f, 0f);
            _world.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = aircraft, Position = away });
            _world.Run(2f);

            Assert.AreEqual(OrderType.Move, _world.Get<ActiveOrder>(aircraft).Value.Type, "the player's move holds");
            Assert.IsFalse(_world.IsEnabled<Docked>(aircraft));
        }

        [Test]
        public void GunshipWithUnlimitedGun_ReturnsWhenItsMissilesAreSpent_AndReloadsThem()
        {
            var (aircraft, tank) = SpawnGunship(missileCooldown: 0.5f);

            Attack(aircraft, tank);
            Assert.IsTrue(_world.RunUntil(() => Missiles(aircraft) == 0, 6f), "fires both missiles");
            Assert.IsTrue(_world.RunUntil(() => _world.IsEnabled<Docked>(aircraft), 8f), "flies home on its gun");

            _world.Run(1.5f);
            Assert.AreEqual(2, Missiles(aircraft), "reloaded while docked");
        }

        [Test]
        public void GunshipWithMissilesLeft_KeepsFighting()
        {
            var (aircraft, tank) = SpawnGunship(missileCooldown: 100f);

            Attack(aircraft, tank);
            Assert.IsTrue(_world.RunUntil(() => Missiles(aircraft) == 1, 6f), "fires one missile");
            var health = _world.GetHealth(tank);
            _world.Run(2f);

            Assert.AreEqual(OrderType.Attack, _world.Get<ActiveOrder>(aircraft).Value.Type, "stays on target");
            Assert.Less(_world.GetHealth(tank), health, "its gun keeps firing");
        }

        [Test]
        public void Airfield_BuildsAircraftDockedOnFreePads_AndWaitsWhenFull()
        {
            var prefab = _world.MakePrefab(_world.AddPadUser(_world.SpawnAircraft(1, float3.zero)));
            _world.SetBuildTime(prefab, 0.5f);
            var airfield = _world.MakeProducer(_world.SpawnAirfield(1, float3.zero, Pad), float3.zero, prefab);
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            _world.Produce(1, airfield, prefab);
            _world.Produce(1, airfield, prefab);

            _world.Run(3f);

            using var built = _world.All<HomePad>();
            Assert.AreEqual(1, built.Length, "one pad, one aircraft");
            Assert.AreEqual(airfield, _world.Get<HomePad>(built[0]).Airfield);
            Assert.IsTrue(_world.IsEnabled<Docked>(built[0]));
            Assert.Less(math.distance(_world.GetPosition(built[0]).xz, Pad.xz), 0.01f, "spawned on its pad");
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<ProductionQueueItem>(airfield).Length, "the next waits");
        }

        [Test]
        public void Airfield_BuildsAircraftOnlyOnPadsOfTheirKind()
        {
            var helicopter = _world.MakePrefab(_world.AddPadUser(_world.SpawnAircraft(1, float3.zero), kind: PadKind.Helipad));
            _world.SetBuildTime(helicopter, 0.5f);
            var shelter = new float3(-5f, 0f, 0f);
            var airfield = _world.MakeProducer(_world.SpawnAirfield(1, float3.zero, shelter, Pad), float3.zero, helicopter);
            var pads = _world.EntityManager.GetBuffer<LandingPad>(airfield);
            pads.ElementAt(0).Kind = PadKind.Shelter;
            pads.ElementAt(1).Kind = PadKind.Helipad;
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            _world.Produce(1, airfield, helicopter);
            _world.Produce(1, airfield, helicopter);

            _world.Run(3f);

            using var built = _world.All<HomePad>();
            Assert.AreEqual(1, built.Length, "the shelter takes no helicopter");
            Assert.AreEqual(1, _world.Get<HomePad>(built[0]).Pad);
            Assert.AreEqual(PadKind.Helipad, _world.Get<HomePad>(built[0]).Kind);
            Assert.Less(math.distance(_world.GetPosition(built[0]).xz, Pad.xz), 0.01f, "spawned on the helipad");
        }

        [Test]
        public void LosingTheAirfield_FreesItsAircraft_AndAnotherAirfieldRehomesThem()
        {
            var first = _world.SpawnAirfield(1, float3.zero, Pad);
            var aircraft = _world.AddPadUser(_world.SpawnAircraft(1, Pad));
            _world.EntityManager.SetComponentData(aircraft, new HomePad { Airfield = first, Pad = 0 });
            _world.ReturnToBase(aircraft);
            _world.Run(1f);
            Assert.IsTrue(_world.IsEnabled<Docked>(aircraft));

            _world.EntityManager.DestroyEntity(first);
            _world.Run(1f);
            Assert.AreEqual(Entity.Null, _world.Get<HomePad>(aircraft).Airfield, "homeless");
            Assert.IsFalse(_world.IsEnabled<Docked>(aircraft));
            Assert.AreEqual(10f, _world.GetPosition(aircraft).y, 1e-3f, "hovering at altitude");

            var second = _world.SpawnAirfield(1, new float3(20f, 0f, 0f), Pad);
            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Unit = aircraft, Target = second });
            Assert.IsTrue(_world.RunUntil(() => _world.IsEnabled<Docked>(aircraft), 6f), "docks");

            Assert.AreEqual(second, _world.Get<HomePad>(aircraft).Airfield);
            Assert.AreEqual(aircraft, _world.EntityManager.GetBuffer<LandingPad>(second)[0].Aircraft);
            Assert.Less(math.distance(_world.GetPosition(aircraft).xz, new float2(25f, 0f)), 0.6f);
        }

        /// <summary>An aircraft with an unlimited gun and a two-round missile mount, and a tank to shoot.</summary>
        private (Entity Aircraft, Entity Tank) SpawnGunship(float missileCooldown)
        {
            var airfield = _world.SpawnAirfield(1, float3.zero, Pad);
            var aircraft = _world.AddWeapon(_world.SpawnAircraft(1, Pad), range: 4f, damage: 1f);
            _world.AddMount(aircraft, range: 4f, damage: 10f, cooldown: missileCooldown, rounds: 2);
            _world.AddPadUser(aircraft);
            _world.EntityManager.SetComponentData(aircraft, new HomePad { Airfield = airfield, Pad = 0 });
            return (aircraft, _world.SpawnUnit(2, new float3(30f, 0f, 0f), maxHealth: 1000f));
        }

        private int Missiles(Entity aircraft) => _world.EntityManager.GetBuffer<WeaponMount>(aircraft)[0].Rounds;

        private void Attack(Entity unit, Entity target) => _world.Command(1, new PlayerCommand
        {
            Type = CommandType.Attack, Unit = unit, Target = target, Position = _world.GetPosition(target),
        });
    }
}
