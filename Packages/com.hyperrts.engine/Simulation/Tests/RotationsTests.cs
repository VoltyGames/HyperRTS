using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    public class RotationsTests
    {
        [Test]
        public void FromTo_TurnsOneVectorOntoTheOther()
        {
            var from = math.normalize(new float3(1f, 2f, -0.5f));
            var to = math.normalize(new float3(-0.3f, 0.1f, 1f));

            AssertNear(to, math.rotate(Rotations.FromTo(from, to), from));
        }

        [Test]
        public void FromTo_SameVector_IsIdentity()
        {
            var rotation = Rotations.FromTo(math.up(), math.up());

            Assert.Greater(math.abs(math.dot(rotation.value, quaternion.identity.value)), 0.9999f);
        }

        [Test]
        public void FromTo_OppositeVectors_TurnHalfwayAboutAPerpendicular()
        {
            var flip = Rotations.FromTo(math.up(), math.down());
            Assert.Greater(math.abs(math.dot(flip.value, quaternion.AxisAngle(math.right(), math.PI).value)), 0.9999f);

            AssertNear(math.left(), math.rotate(Rotations.FromTo(math.right(), math.left()), math.right()));
        }

        private static void AssertNear(float3 expected, float3 actual) =>
            Assert.Less(math.distance(expected, actual), 1e-4f, $"expected {expected}, was {actual}");
    }
}
