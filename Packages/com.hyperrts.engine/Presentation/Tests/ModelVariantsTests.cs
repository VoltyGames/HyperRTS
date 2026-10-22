using System.Collections.Generic;
using HyperRTS.Presentation.Rendering;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The position-hashed model variant pick every client must agree on.</summary>
    public class ModelVariantsTests
    {
        private static readonly ModelVariants Three = new() { Count = 3, Shown = -1 };

        [Test]
        public void Pick_SurvivesReplicationRounding()
        {
            for (var i = 0; i < 20; i++)
            {
                var center = new float3(4f + 2f * i, 1.3f, 0.5f);
                var replicated = center + new float3(0.004f, 0.2f, -0.003f);
                Assert.AreEqual(Three.Pick(center), Three.Pick(replicated), $"segment {i}");
            }
        }

        [Test]
        public void Pick_StaysInRange_AndVariesAlongARow()
        {
            var seen = new HashSet<int>();
            for (var i = 0; i < 12; i++)
            {
                var pick = Three.Pick(new float3(4f + 2f * i, 0f, 0.5f));
                Assert.That(pick, Is.InRange(0, 2));
                seen.Add(pick);
            }

            Assert.Greater(seen.Count, 1, "a wall line doesn't repeat one panel");
        }
    }
}
