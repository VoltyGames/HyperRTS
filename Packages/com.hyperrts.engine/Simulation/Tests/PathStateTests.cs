using HyperRTS.Simulation.Auras;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The cached path length in <see cref="PathState"/>, instant handling and kinds.</summary>
    public class PathStateTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp() => _world = new TestWorld();

        [TearDown]
        public void TearDown() => _world.Dispose();

        private DynamicBuffer<PathWaypoint> Path(params float2[] corners)
        {
            var waypoints = _world.EntityManager.AddBuffer<PathWaypoint>(_world.EntityManager.CreateEntity());
            foreach (var corner in corners)
            {
                waypoints.Add(new PathWaypoint { Position = new float3(corner.x, 0f, corner.y) });
            }

            return waypoints;
        }

        [Test]
        public void LengthPastCorner_StaysTheSum_AsCornersPassAndTheGoalMoves()
        {
            var waypoints = Path(new float2(0f, 0f), new float2(3f, 0f), new float2(3f, 4f));
            var path = new PathState { LengthPastCorner = PathState.Measure(waypoints) };
            Assert.AreEqual(7f, path.LengthPastCorner, 1e-4f);

            path.MoveLast(waypoints, new float2(3f, 6f));
            Assert.AreEqual(PathState.Measure(waypoints), path.LengthPastCorner, 1e-4f, "goal nudged");

            path.PassCorner(waypoints);
            Assert.AreEqual(6f, path.LengthPastCorner, 1e-4f, "first corner passed");

            path.ClearWaypoints(waypoints);
            Assert.AreEqual(0f, path.LengthPastCorner);
        }

        [Test]
        public void InstantLocomotion_StepsAtFullSpeed_ButNeverPastTheGoal()
        {
            var instant = Locomotion.Instant;
            var state = default(LocomotionState);
            var facing = new float2(1f, 0f);
            var far = new PathLeg(float2.zero, new float2(10f, 0f), 0f, 10f, true);
            var step = instant.Drive(ref state, float2.zero, ref facing, far, 5f, 0.4f, 0.1f);
            MathAssert.Near(new float2(0.5f, 0f), step);

            var near = new PathLeg(float2.zero, new float2(0.2f, 0f), 0f, 0.2f, true);
            step = instant.Drive(ref state, float2.zero, ref facing, near, 5f, 0.4f, 0.1f);
            MathAssert.Near(new float2(0.2f, 0f), step);
        }

        [Test]
        public void Fields_AffectUnitsAndBuildings_ButNotScenery()
        {
            var relations = default(FactionRelations);
            var everything = FieldTargets.Own | FieldTargets.AllKinds;
            Assert.IsTrue(FieldRules.Affects(everything, 1, new SpatialEntry { Faction = 1, IsUnit = true }, relations));
            Assert.IsTrue(FieldRules.Affects(everything, 1, new SpatialEntry { Faction = 1, IsBuilding = true },
                relations));
            Assert.IsFalse(FieldRules.Affects(everything, 1, new SpatialEntry { Faction = 1 }, relations), "scenery");
        }
    }
}
