using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Air
{
    /// <summary>
    /// Airfield pads, the reload cycle (out of ammo or mount rounds, return, dock, reload) and losing or changing
    /// airfields.
    /// </summary>
    public class AirfieldTests : SimulationFixture
    {
        private static readonly float3 Pad = new(5f, 0f, 0f);

        [Test]
        public void AircraftOutOfAmmo_ReturnsToItsPad_ReloadsAndIsReadyAgain()
        {
            var airfield = World.SpawnAirfield(1, float3.zero, Pad);
            var aircraft = World.AddWeapon(World.SpawnAircraft(1, Pad), range: 4f, damage: 10f);
            World.AddPadUser(aircraft, rounds: 2);
            World.Set(aircraft, new HomePad { Airfield = airfield, Pad = 0 });
            var tank = World.SpawnUnit(2, new float3(30f, 0f, 0f));

            Attack(aircraft, tank);
            Assert.IsTrue(World.RunUntil(() => World.PrimaryAmmo(aircraft).Current == 0, 6f), "fires every round");
            Assert.IsTrue(World.RunUntil(() => World.IsEnabled<Docked>(aircraft), 8f), "flies home and docks");

            Assert.AreEqual(80f, World.GetHealth(tank), "two rounds, then it held fire");
            Assert.Less(math.distance(World.GetPosition(aircraft).xz, Pad.xz), 0.6f, "back on its pad");
            Assert.AreEqual(aircraft, World.Buffer<LandingPad>(airfield)[0].Reservation.Holder);

            World.Run(1.5f);
            Assert.AreEqual(0f, World.GetPosition(aircraft).y, 1e-3f, "landed");
            Assert.AreEqual(2, World.PrimaryAmmo(aircraft).Current, "reloaded while docked");

            Attack(aircraft, tank);
            World.Run(4f);
            Assert.IsFalse(World.IsEnabled<Docked>(aircraft), "took off again");
            Assert.AreEqual(60f, World.GetHealth(tank), "and fired its fresh rounds");
        }

        [Test]
        public void SpentAircraft_IsSentHomeOnce_ThenObeysPlayerOrders()
        {
            var airfield = World.SpawnAirfield(1, float3.zero, Pad);
            var aircraft = World.AddWeapon(World.SpawnAircraft(1, Pad), range: 4f, damage: 10f);
            World.AddPadUser(aircraft, rounds: 1);
            World.Set(aircraft, new HomePad { Airfield = airfield, Pad = 0 });
            var tank = World.SpawnUnit(2, new float3(30f, 0f, 0f));

            Attack(aircraft, tank);
            Assert.IsTrue(World.RunUntil(() => World.PrimaryAmmo(aircraft).Current == 0, 6f), "fires its round");
            World.Tick();
            Assert.AreEqual(OrderType.ReturnToBase, World.Get<ActiveOrder>(aircraft).Value.Type, "sent home");

            var away = new float3(-40f, 0f, 0f);
            World.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = aircraft, Position = away });
            World.Run(2f);

            Assert.AreEqual(OrderType.Move, World.Get<ActiveOrder>(aircraft).Value.Type, "the player's move holds");
            Assert.IsFalse(World.IsEnabled<Docked>(aircraft));
        }

        [Test]
        public void GunshipWithUnlimitedGun_ReturnsWhenItsMissilesAreSpent_AndReloadsThem()
        {
            var (aircraft, tank) = SpawnGunship(missileCooldown: 0.5f);

            Attack(aircraft, tank);
            Assert.IsTrue(World.RunUntil(() => Missiles(aircraft) == 0, 6f), "fires both missiles");
            Assert.IsTrue(World.RunUntil(() => World.IsEnabled<Docked>(aircraft), 8f), "flies home on its gun");

            World.Run(1.5f);
            Assert.AreEqual(2, Missiles(aircraft), "reloaded while docked");
        }

        [Test]
        public void GunshipWithMissilesLeft_KeepsFighting()
        {
            var (aircraft, tank) = SpawnGunship(missileCooldown: 100f);

            Attack(aircraft, tank);
            Assert.IsTrue(World.RunUntil(() => Missiles(aircraft) == 1, 6f), "fires one missile");
            var health = World.GetHealth(tank);
            World.Run(2f);

            Assert.AreEqual(OrderType.Attack, World.Get<ActiveOrder>(aircraft).Value.Type, "stays on target");
            Assert.Less(World.GetHealth(tank), health, "its gun keeps firing");
        }

        [Test]
        public void Airfield_BuildsAircraftDockedOnFreePads_AndWaitsWhenFull()
        {
            var prefab = World.MakePrefab(World.AddPadUser(World.SpawnAircraft(1, float3.zero)));
            World.SetBuildTime(prefab, 0.5f);
            var airfield = World.MakeProducer(World.SpawnAirfield(1, float3.zero, Pad), float3.zero, prefab);
            World.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            World.Produce(1, airfield, prefab);
            World.Produce(1, airfield, prefab);

            World.Run(3f);

            using var built = World.All<HomePad>();
            Assert.AreEqual(1, built.Length, "one pad, one aircraft");
            Assert.AreEqual(airfield, World.Get<HomePad>(built[0]).Airfield);
            Assert.IsTrue(World.IsEnabled<Docked>(built[0]));
            Assert.Less(math.distance(World.GetPosition(built[0]).xz, Pad.xz), 0.01f, "spawned on its pad");
            Assert.AreEqual(1, World.Buffer<ProductionQueueItem>(airfield).Length, "the next waits");
        }

        [Test]
        public void Airfield_BuildsAircraftOnlyOnPadsOfTheirKind()
        {
            var helicopter = World.MakePrefab(World.AddPadUser(World.SpawnAircraft(1, float3.zero), kind: PadKind.Helipad));
            World.SetBuildTime(helicopter, 0.5f);
            var shelter = new float3(-5f, 0f, 0f);
            var airfield = World.MakeProducer(World.SpawnAirfield(1, float3.zero, shelter, Pad), float3.zero, helicopter);
            var pads = World.Buffer<LandingPad>(airfield);
            pads.ElementAt(0).Kind = PadKind.Shelter;
            pads.ElementAt(1).Kind = PadKind.Helipad;
            World.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            World.Produce(1, airfield, helicopter);
            World.Produce(1, airfield, helicopter);

            World.Run(3f);

            using var built = World.All<HomePad>();
            Assert.AreEqual(1, built.Length, "the shelter takes no helicopter");
            Assert.AreEqual(1, World.Get<HomePad>(built[0]).Pad);
            Assert.AreEqual(PadKind.Helipad, World.Get<HomePad>(built[0]).Kind);
            Assert.Less(math.distance(World.GetPosition(built[0]).xz, Pad.xz), 0.01f, "spawned on the helipad");
        }

        [Test]
        public void LosingTheAirfield_FreesItsAircraft_AndAnotherAirfieldRehomesThem()
        {
            var first = World.SpawnAirfield(1, float3.zero, Pad);
            var aircraft = World.AddPadUser(World.SpawnAircraft(1, Pad));
            World.Set(aircraft, new HomePad { Airfield = first, Pad = 0 });
            World.ReturnToBase(aircraft);
            World.Run(1f);
            Assert.IsTrue(World.IsEnabled<Docked>(aircraft));

            World.EntityManager.DestroyEntity(first);
            World.Run(1f);
            Assert.AreEqual(Entity.Null, World.Get<HomePad>(aircraft).Airfield, "homeless");
            Assert.IsFalse(World.IsEnabled<Docked>(aircraft));
            Assert.AreEqual(10f, World.GetPosition(aircraft).y, 1e-3f, "hovering at altitude");

            var second = World.SpawnAirfield(1, new float3(20f, 0f, 0f), Pad);
            World.Command(1, new PlayerCommand { Type = CommandType.Smart, Unit = aircraft, Target = second });
            Assert.IsTrue(World.RunUntil(() => World.IsEnabled<Docked>(aircraft), 6f), "docks");

            Assert.AreEqual(second, World.Get<HomePad>(aircraft).Airfield);
            Assert.AreEqual(aircraft, World.Buffer<LandingPad>(second)[0].Reservation.Holder);
            Assert.Less(math.distance(World.GetPosition(aircraft).xz, new float2(25f, 0f)), 0.6f);
        }

        [Test]
        public void AircraftDyingOnItsPad_FreesThePad()
        {
            var airfield = World.SpawnAirfield(1, float3.zero, Pad);
            var aircraft = World.AddPadUser(World.SpawnAircraft(1, Pad));
            World.Set(aircraft, new HomePad { Airfield = airfield, Pad = 0 });
            World.Run(0.5f);
            Assert.AreEqual(aircraft, World.Buffer<LandingPad>(airfield)[0].Reservation.Holder);

            World.EntityManager.DestroyEntity(aircraft);
            World.Run(0.5f);

            Assert.IsTrue(World.Buffer<LandingPad>(airfield)[0].Reservation.IsFree);
        }

        /// <summary>An aircraft with an unlimited gun and a two-round missile mount, and a tank to shoot.</summary>
        private (Entity Aircraft, Entity Tank) SpawnGunship(float missileCooldown)
        {
            var airfield = World.SpawnAirfield(1, float3.zero, Pad);
            var aircraft = World.AddWeapon(World.SpawnAircraft(1, Pad), range: 4f, damage: 1f);
            World.AddMount(aircraft, range: 4f, damage: 10f, cooldown: missileCooldown, rounds: 2);
            World.AddPadUser(aircraft);
            World.Set(aircraft, new HomePad { Airfield = airfield, Pad = 0 });
            return (aircraft, World.SpawnUnit(2, new float3(30f, 0f, 0f), maxHealth: 1000f));
        }

        private int Missiles(Entity aircraft) => World.Mount(aircraft).Ammo.Current;

        private void Attack(Entity unit, Entity target) => World.Command(1, new PlayerCommand
        {
            Type = CommandType.Attack, Unit = unit, Target = target, Position = World.GetPosition(target),
        });
    }
}
