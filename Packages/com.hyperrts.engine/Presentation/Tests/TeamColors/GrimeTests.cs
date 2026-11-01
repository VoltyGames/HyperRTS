using HyperRTS.Presentation.TeamColors;
using HyperRTS.Simulation.Cues.Surfaces;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests.TeamColors
{
    /// <summary>Grime: each channel rises from its cause and clamps to 0..1.</summary>
    public class GrimeTests
    {
        private const float Dry = 0f;
        private const float Wet = 1f;

        [Test]
        public void Dust_RisesWithDistanceOnDrySoil_NotOnPaved_OrWetSoil()
        {
            var soil = Drive(SurfaceMaterial.Soil, Dry, 200f);
            Assert.AreEqual(2f * Grime.Standard.DustPer100m, soil.Dust, 1e-4f);
            Assert.AreEqual(0f, Drive(SurfaceMaterial.Paved, Dry, 200f).Dust);
            Assert.AreEqual(0f, Drive(SurfaceMaterial.Soil, Wet, 200f).Dust);
        }

        [Test]
        public void Mud_RisesOnMud_AndOnSoilOnlyWhileWet()
        {
            Assert.Greater(Drive(SurfaceMaterial.Mud, Dry, 100f).Mud, 0f);
            Assert.Greater(Drive(SurfaceMaterial.Soil, Wet, 100f).Mud, 0f);
            Assert.AreEqual(0f, Drive(SurfaceMaterial.Soil, Dry, 100f).Mud);
        }

        [Test]
        public void Wet_JumpsInWater_ThenDriesOverTheDryingTime()
        {
            var grime = Grime.Standard;
            grime.Advance(float3.zero, SurfaceMaterial.Water, Dry, 1f, 0.1f);
            Assert.AreEqual(1f, grime.Wet);

            grime.Advance(float3.zero, SurfaceMaterial.Soil, Dry, 1f, grime.DryingSeconds / 2f);
            Assert.AreEqual(0.5f, grime.Wet, 1e-4f);
            grime.Advance(float3.zero, SurfaceMaterial.Soil, Dry, 1f, grime.DryingSeconds);
            Assert.AreEqual(0f, grime.Wet);
        }

        [Test]
        public void Water_WashesDustFast_AndMudPartly()
        {
            var grime = Grime.Standard;
            grime.Dust = 1f;
            grime.Mud = 1f;
            grime.Advance(float3.zero, SurfaceMaterial.Water, Dry, 1f, 1f);
            Assert.AreEqual(0.5f, grime.Dust, 1e-4f);
            Assert.AreEqual(0.9f, grime.Mud, 1e-4f);
        }

        [Test]
        public void Soot_FollowsDamage_AndNeverFalls()
        {
            var grime = Grime.Standard;
            grime.Advance(float3.zero, SurfaceMaterial.Soil, Dry, 0.4f, 0.1f);
            Assert.AreEqual(0.6f, grime.Soot, 1e-4f);

            grime.Advance(float3.zero, SurfaceMaterial.Soil, Dry, 1f, 0.1f);
            Assert.AreEqual(0.6f, grime.Soot, 1e-4f, "repairs leave the soot");
        }

        [Test]
        public void Levels_ClampToOne_AndRewriteOnlyAfterAStep()
        {
            var grime = Grime.Standard;
            grime.Advance(float3.zero, SurfaceMaterial.Sand, Dry, 1f, 0.1f);
            grime.Paint();

            Assert.IsFalse(grime.Advance(new float3(1f, 0f, 0f), SurfaceMaterial.Sand, Dry, 1f, 0.1f), "a metre is a small step");
            grime.Advance(new float3(1e5f, 0f, 0f), SurfaceMaterial.Sand, Dry, 1f, 0.1f);
            Assert.AreEqual(1f, grime.Dust);
        }

        // Starts at the origin and drives straight for the distance in one frame.
        private static Grime Drive(SurfaceMaterial ground, float wetness, float metres)
        {
            var grime = Grime.Standard;
            grime.Advance(float3.zero, ground, wetness, 1f, 0.1f);
            grime.Advance(new float3(metres, 0f, 0f), ground, wetness, 1f, 0.1f);
            return grime;
        }
    }
}
