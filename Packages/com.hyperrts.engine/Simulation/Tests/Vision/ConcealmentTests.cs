using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Vision
{
    /// <summary>Woods and tall crops thin sight to nothing, vehicles flatten crops, and still infantry hide in cover.</summary>
    public class ConcealmentTests
    {
        private const byte Team1 = 1;
        private const byte Team2 = 2;

        // Fog cells are 2 m from -100, so odd coordinates are cell centres.
        private static readonly SightBlock Woods = new()
        {
            Size = new float2(20f, 40f), Density = 0.1f, PorousTop = 10f,
        };

        private TestWorld _world;
        private CropCover _crops;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(Team1, Team2);
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            if (_crops.Blob.IsCreated)
            {
                _crops.Blob.Dispose();
            }
        }

        [TestCase(2f, false, TestName = "TenMetresOfWoods_StopSight")]
        [TestCase(12f, true, TestName = "ViewerAboveTheCanopy_SeesOverTheWoods")]
        public void Woods_StopSight_UnlessTheEyeIsAboveThem(float eye, bool seesPast)
        {
            _world.SpawnViewer(Team1, new float3(1f, 0f, 1f), 40f, eye);
            _world.SpawnSightBlock(new float3(15f, 0f, 1f), Woods);

            _world.Tick();

            var fog = _world.Fog();
            Assert.IsTrue(fog.IsVisible(new float3(9f, 0f, 1f), Team1, false), "the edge of the woods");
            Assert.AreEqual(seesPast, fog.IsVisible(new float3(19f, 0f, 1f), Team1, false), "deep in the woods");
            Assert.AreEqual(seesPast, fog.IsVisible(new float3(31f, 0f, 1f), Team1, false), "past the woods");
        }

        [Test]
        public void SunflowerField_HidesATeamInIt_UntilAVehicleFlattensALane()
        {
            AddCropField(new float2(5f, -20f), new int2(40, 40), density: 0.05f, top: 2.5f);
            _world.SpawnViewer(Team1, new float3(1f, 0f, 1f), 40f);
            var team = _world.SpawnUnit(Team2, new float3(37f, 0f, 1f));
            _world.Tick(frames: 2);
            Assert.IsTrue(_world.IsFogHidden(team), "30 m of sunflowers hide it");

            var tank = _world.SpawnUnit(Team1, new float3(3f, 0f, 1f), radius: 1.5f);
            _world.EntityManager.AddComponentData(tank, new Crusher { Size = 3 });
            _world.EntityManager.SetComponentData(tank, new VisionRange());
            _world.MoveTo(tank, new float3(30f, 0f, 1f));
            Assert.IsTrue(_world.RunUntil(() => _world.Get<Unity.Transforms.LocalTransform>(tank).Position.x > 28f, 20f));
            _world.Run(FogOfWarSystem.UpdateInterval * 2f);

            Assert.IsFalse(_world.IsFogHidden(team), "the lane opens a line of sight");
        }

        [Test]
        public void StillInfantryInWoods_HideBeyondTheRevealDistance_AndShowAfterFiring()
        {
            _world.SpawnSightBlock(new float3(31f, 0f, 1f), Woods);
            var viewer = _world.SpawnViewer(Team1, new float3(1f, 0f, 1f), 40f);
            var squad = Concealable(_world.SpawnUnit(Team2, new float3(23f, 0f, 1f)));

            _world.Run(0.3f);
            Assert.IsTrue(_world.IsEnabled<Concealed>(squad), "still in the woods");
            Assert.IsTrue(_world.IsFogHidden(squad), "hidden 22 m away though the woods' edge is in sight");

            _world.EntityManager.SetComponentData(viewer, Unity.Transforms.LocalTransform.FromPosition(new float3(11f, 0f, 1f)));
            _world.Run(0.3f);
            Assert.IsFalse(_world.IsFogHidden(squad), "shown 12 m away");

            _world.EntityManager.SetComponentData(viewer, Unity.Transforms.LocalTransform.FromPosition(new float3(1f, 0f, 1f)));
            var stealth = _world.Get<Stealth>(squad);
            stealth.RevealTimer = 3f;
            _world.EntityManager.SetComponentData(squad, stealth);
            _world.Run(0.3f);
            Assert.IsFalse(_world.IsFogHidden(squad), "shown after firing");

            _world.Run(3f);
            Assert.IsTrue(_world.IsFogHidden(squad), "hidden again once the reveal ends");
        }

        [TestCase(false, true, TestName = "Aircraft_DontSpotConcealedInfantry")]
        [TestCase(true, false, TestName = "AircraftWithADetector_SpotConcealedInfantry")]
        public void Aircraft_SpotConcealedInfantry_OnlyWithADetector(bool detector, bool hidden)
        {
            _world.SpawnSightBlock(new float3(31f, 0f, 1f), Woods);
            var drone = _world.SpawnViewer(Team1, new float3(23f, 0f, 3f), 40f);
            _world.EntityManager.SetComponentData(drone, new NavAgent { Radius = 0.5f, Layer = NavLayer.Air });
            if (detector)
            {
                var writer = new EntityManagerWriter(_world.EntityManager, drone);
                StealthSetup.AddDetector(ref writer, 20f);
            }

            var squad = Concealable(_world.SpawnUnit(Team2, new float3(23f, 0f, 1f)));

            _world.Run(0.3f);

            Assert.AreEqual(hidden, _world.IsFogHidden(squad));
        }

        private Entity Concealable(Entity unit)
        {
            var writer = new EntityManagerWriter(_world.EntityManager, unit);
            StealthSetup.AddStealth(ref writer, new Stealth { RevealDuration = 3f, OnlyWhenStill = true, InConcealment = true },
                true);
            return unit;
        }

        /// <summary>A rectangle of 1 m crop cells from its south-west corner, as the map build paints a field.</summary>
        private void AddCropField(float2 min, int2 size, float density, float top)
        {
            var count = size.x * size.y;
            using var densities = new NativeArray<byte>(Filled(count, density / SightBlockers.DensityStep), Allocator.Temp);
            using var tops = new NativeArray<byte>(Filled(count, top / SightBlockers.Step), Allocator.Temp);

            _crops = CropCover.Create(densities, tops, size, min, 1f, Allocator.Persistent);
            _world.EntityManager.CreateSingleton(_crops);
        }

        private static byte[] Filled(int count, float steps)
        {
            var cells = new byte[count];
            System.Array.Fill(cells, (byte)math.round(steps));
            return cells;
        }
    }
}
