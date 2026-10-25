using HyperRTS.Presentation.Environment.Grass;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The building footprint mask grass and crops skip: drawn from turned boxes, redrawn when buildings go.</summary>
    public class BuildingFootprintSystemTests
    {
        private PresentationWorldKit _kit;
        private EntityManager _em;
        private BuildingFootprintSystem _system;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Building Footprint Tests");
            _em = _kit.EntityManager;
            _system = _kit.World.CreateSystemManaged<BuildingFootprintSystem>();
            var map = _em.CreateEntity();
            _em.AddComponentData(map, new MapSettings { Min = new float2(-50f), Size = new float2(100f), NavCellSize = 1f });
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        private Entity Building(float3 position, float yaw, float2 size)
        {
            var building = _em.CreateEntity();
            _em.AddComponent<BuildingTag>(building);
            _em.AddComponentData(building, new NavObstacle { Size = size });
            _em.AddComponentData(building, LocalTransform.FromPositionRotation(position, quaternion.RotateY(yaw)));
            return building;
        }

        [Test]
        public void TurnedBuilding_MarksTheCellsUnderItsTurnedBox()
        {
            Building(float3.zero, math.PI / 4f, new float2(10f, 2f));

            _system.Update();

            var mask = _system.Mask;
            Assert.IsTrue(mask.IsMarked(float2.zero), "under its middle");
            Assert.IsTrue(mask.IsMarked(new float2(3f, -3f)), "along its turned length");
            Assert.IsFalse(mask.IsMarked(new float2(3f, 3f)), "off its side, inside its world box");
            Assert.IsFalse(mask.IsMarked(new float2(20f, 20f)), "open ground");
        }

        [Test]
        public void DestroyedBuilding_ClearsItsCells_AndBumpsTheVersion()
        {
            var building = Building(new float3(10f, 0f, 10f), 0f, new float2(6f, 6f));
            _system.Update();
            var drawn = BuildingFootprintMask.Version;

            _em.DestroyEntity(building);
            _system.Update();

            Assert.IsFalse(_system.Mask.IsMarked(new float2(10f, 10f)));
            Assert.AreNotEqual(drawn, BuildingFootprintMask.Version, "scattered plants regrow");
        }

        [Test]
        public void UnchangedBuildings_DoNotRedraw()
        {
            Building(float3.zero, 0f, new float2(4f, 4f));
            _system.Update();
            var drawn = BuildingFootprintMask.Version;

            _system.Update();

            Assert.AreEqual(drawn, BuildingFootprintMask.Version);
        }
    }
}
