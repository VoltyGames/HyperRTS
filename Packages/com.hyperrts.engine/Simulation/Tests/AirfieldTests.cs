using System;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Airfield pads, the rearm cycle (out of ammo, return, dock, reload) and losing or changing airfields.</summary>
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
            var aircraft = _world.UsePads(_world.Arm(_world.SpawnAircraft(1, Pad), range: 4f, damage: 10f), rounds: 2);
            _world.EntityManager.SetComponentData(aircraft, new PadHome { Airfield = airfield, Pad = 0 });
            var tank = _world.SpawnUnit(2, new float3(30f, 0f, 0f));

            Attack(aircraft, tank);
            RunUntil(() => _world.Get<Ammo>(aircraft).Current == 0, 6f);
            RunUntil(() => _world.IsEnabled<Docked>(aircraft), 8f);

            Assert.AreEqual(80f, _world.HealthOf(tank), "two rounds, then it held fire");
            Assert.Less(math.distance(_world.PositionOf(aircraft).xz, Pad.xz), 0.6f, "back on its pad");
            Assert.AreEqual(aircraft, _world.EntityManager.GetBuffer<LandingPad>(airfield)[0].Aircraft);

            _world.Run(1.5f);
            Assert.AreEqual(0f, _world.PositionOf(aircraft).y, 1e-3f, "landed");
            Assert.AreEqual(2, _world.Get<Ammo>(aircraft).Current, "reloaded while docked");

            Attack(aircraft, tank);
            _world.Run(4f);
            Assert.IsFalse(_world.IsEnabled<Docked>(aircraft), "took off again");
            Assert.AreEqual(60f, _world.HealthOf(tank), "and fired its fresh rounds");
        }

        [Test]
        public void Airfield_BuildsAircraftDockedOnFreePads_AndWaitsWhenFull()
        {
            var prefab = _world.MakePrefab(_world.UsePads(_world.SpawnAircraft(1, float3.zero)));
            _world.SetBuildTime(prefab, 0.5f);
            var airfield = _world.MakeProducer(_world.SpawnAirfield(1, float3.zero, Pad), float3.zero, prefab);
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            _world.Produce(1, airfield, prefab);
            _world.Produce(1, airfield, prefab);

            _world.Run(3f);

            using var built = _world.All<PadHome>();
            Assert.AreEqual(1, built.Length, "one pad, one aircraft");
            Assert.AreEqual(airfield, _world.Get<PadHome>(built[0]).Airfield);
            Assert.IsTrue(_world.IsEnabled<Docked>(built[0]));
            Assert.Less(math.distance(_world.PositionOf(built[0]).xz, Pad.xz), 0.01f, "spawned on its pad");
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<ProductionQueueItem>(airfield).Length, "the next waits");
        }

        [Test]
        public void LosingTheAirfield_FreesItsAircraft_AndAnotherAirfieldRehomesThem()
        {
            var first = _world.SpawnAirfield(1, float3.zero, Pad);
            var aircraft = _world.UsePads(_world.SpawnAircraft(1, Pad));
            _world.EntityManager.SetComponentData(aircraft, new PadHome { Airfield = first, Pad = 0 });
            _world.ReturnToBase(aircraft);
            _world.Run(1f);
            Assert.IsTrue(_world.IsEnabled<Docked>(aircraft));

            _world.EntityManager.DestroyEntity(first);
            _world.Run(1f);
            Assert.AreEqual(Entity.Null, _world.Get<PadHome>(aircraft).Airfield, "homeless");
            Assert.IsFalse(_world.IsEnabled<Docked>(aircraft));
            Assert.AreEqual(10f, _world.PositionOf(aircraft).y, 1e-3f, "hovering at altitude");

            var second = _world.SpawnAirfield(1, new float3(20f, 0f, 0f), Pad);
            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Unit = aircraft, Target = second });
            RunUntil(() => _world.IsEnabled<Docked>(aircraft), 6f);

            Assert.AreEqual(second, _world.Get<PadHome>(aircraft).Airfield);
            Assert.AreEqual(aircraft, _world.EntityManager.GetBuffer<LandingPad>(second)[0].Aircraft);
            Assert.Less(math.distance(_world.PositionOf(aircraft).xz, new float2(25f, 0f)), 0.6f);
        }

        private void Attack(Entity unit, Entity target) => _world.Command(1, new PlayerCommand
        {
            Type = CommandType.Attack, Unit = unit, Target = target, Position = _world.PositionOf(target),
        });

        private void RunUntil(Func<bool> done, float seconds)
        {
            for (var t = 0f; t < seconds && !done(); t += TestWorld.FrameTime)
            {
                _world.Tick();
            }

            Assert.IsTrue(done(), $"not reached within {seconds} s");
        }
    }
}
