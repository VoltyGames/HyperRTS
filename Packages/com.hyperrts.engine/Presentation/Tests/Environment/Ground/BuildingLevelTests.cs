using HyperRTS.Presentation.Environment.Ground.Craters;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests.Environment.Ground
{
    /// <summary>How far the drawn ground is levelled around a building's rotated footprint.</summary>
    public class BuildingLevelTests
    {
        private static readonly float2 East = new(1f, 0f);

        [Test]
        public void Inside_IsFullyLevelled_AndPastTheSkirtNot()
        {
            var level = new BuildingLevel(new float2(10f, 0f), East, new float2(8f, 4f), 1f);

            Assert.AreEqual(1f, level.Weight(new float2(10f, 0f)));
            Assert.AreEqual(1f, level.Weight(new float2(14f, 2f)), "the edge is inside");
            Assert.AreEqual(0f, level.Weight(new float2(14f + BuildingLevel.Skirt, 0f)));
            Assert.AreEqual(0f, level.Weight(new float2(30f, 30f)));
        }

        [Test]
        public void Skirt_EasesDownWithoutAStep()
        {
            var level = new BuildingLevel(float2.zero, East, new float2(8f, 8f), 0f);

            Assert.AreEqual(0.5f, level.Weight(new float2(4f + BuildingLevel.Skirt * 0.5f, 0f)), 1e-5f);
            Assert.Greater(level.Weight(new float2(4.1f, 0f)), 0.99f);
            Assert.Less(level.Weight(new float2(4f + BuildingLevel.Skirt - 0.1f, 0f)), 0.01f);
        }

        [Test]
        public void TurnedFootprint_FollowsItsAxes()
        {
            var level = new BuildingLevel(float2.zero, new float2(0f, 1f), new float2(8f, 2f), 0f);

            Assert.AreEqual(1f, level.Weight(new float2(0.5f, 3.5f)), "its width runs north");
            Assert.AreEqual(0f, level.Weight(new float2(1f + BuildingLevel.Skirt, 0f)), "past its narrow side");
            Assert.AreEqual(new float2(1f, 4f) + BuildingLevel.Skirt, level.Reach);
        }

        [Test]
        public void Target_SitsBelowTheBase()
        {
            var level = new BuildingLevel(float2.zero, East, new float2(2f, 2f), 5f);

            Assert.AreEqual(5f - BuildingLevel.Drop, level.Target, 1e-6f);
        }
    }
}
