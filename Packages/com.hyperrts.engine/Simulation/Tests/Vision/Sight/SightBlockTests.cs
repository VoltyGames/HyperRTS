using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Vision.Fog;
using HyperRTS.Simulation.Vision.Sight;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Vision.Sight
{
    /// <summary>Solid sight blocks: shadows behind buildings and walls, garrisons on the roof, and fallen blocks.</summary>
    public class SightBlockTests
    {
        private const byte Team1 = 1;

        // Fog cells are 2 m from -100, so odd coordinates are cell centres.
        private static readonly float3 Viewer = new(1f, 0f, 1f);
        private static readonly float3 Behind = new(21f, 0f, 1f);

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(Team1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Building_HidesTheCellsBehindIt_NotInFront()
        {
            _world.SpawnViewer(Team1, Viewer, 30f);
            SpawnBlock(new float3(11f, 0f, 1f), new float2(4f, 4f), 6f);

            _world.Tick();

            var fog = _world.Fog();
            Assert.IsTrue(fog.IsVisible(new float3(5f, 0f, 1f), Team1, false), "in front of the building");
            Assert.IsTrue(fog.IsVisible(new float3(11f, 0f, 1f), Team1, false), "its roof");
            Assert.That(fog.IsVisible(Behind, Team1, false), Is.False, () => "behind it\n" + GridDump.Fog(fog, Team1, Behind, 12));
            Assert.IsTrue(fog.IsVisible(new float3(1f, 0f, 15f), Team1, false), "open ground beside it");
        }

        [TestCase(2f, false, TestName = "Wall_HidesTheGroundBehindIt")]
        [TestCase(8f, true, TestName = "Wall_HidesNothing_FromAViewerTallerThanIt")]
        public void Wall_HidesTheGround_FromViewersBelowItsTop(float eye, bool seen)
        {
            _world.SpawnViewer(Team1, Viewer, 30f, eye);
            SpawnBlock(new float3(7f, 0f, 1f), new float2(0.4f, 10f), 2.5f);

            _world.Tick();

            Assert.AreEqual(seen, _world.Fog().IsVisible(new float3(15f, 0f, 1f), Team1, false));
        }

        [TestCase(true, TestName = "Garrison_SeesTheStreetAndOverNearbyRoofs")]
        [TestCase(false, TestName = "SquadAtTheSameSpot_OutsideTheHouse_SeesNothingPastItsWalls")]
        public void Garrison_SeesFromAboveItsRoof(bool inside)
        {
            var house = SpawnBlock(Viewer, new float2(6f, 6f), 6f);
            _world.Add(house, new PassengerEye { AboveTop = 0.5f });
            SpawnBlock(new float3(11f, 0f, 1f), new float2(2f, 8f), 3f);
            var squad = _world.SpawnViewer(Team1, Viewer, 30f);
            if (inside)
            {
                _world.Add(squad, new Inside { Container = house });
            }

            _world.Tick();

            var fog = _world.Fog();
            Assert.AreEqual(inside, fog.IsVisible(new float3(7f, 0f, 1f), Team1, false), "the street by the house");
            Assert.AreEqual(inside, fog.IsVisible(Behind, Team1, false), "behind the lower house next door");
        }

        [TestCase(true, TestName = "DestroyedBuilding_StopsBlocking_AtTheNextStamp")]
        [TestCase(false, TestName = "DisabledBlock_StopsBlocking_AtTheNextStamp")]
        public void RemovedBlock_StopsBlocking_AtTheNextStamp(bool destroyed)
        {
            _world.SpawnViewer(Team1, Viewer, 30f);
            var building = SpawnBlock(new float3(11f, 0f, 1f), new float2(4f, 4f), 6f);
            _world.Tick();
            Assert.IsFalse(_world.Fog().IsVisible(Behind, Team1, false), "hidden while it stands");

            if (destroyed)
            {
                _world.EntityManager.DestroyEntity(building);
            }
            else
            {
                _world.SetEnabled<SightBlock>(building, false);
            }

            _world.Run(FogOfWarSystem.UpdateInterval * 2f);

            Assert.IsTrue(_world.Fog().IsVisible(Behind, Team1, false));
        }

        private Entity SpawnBlock(float3 position, float2 size, float height) =>
            _world.SpawnSightBlock(position, size, height);
    }
}
