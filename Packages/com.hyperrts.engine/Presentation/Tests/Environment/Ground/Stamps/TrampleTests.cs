using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Presentation.Environment.Ground.Stamps;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Tests.Environment.Ground.Stamps
{
    /// <summary>Trample maps on the CPU: texture sizing, fades, and the stamps vehicles, craters, wakes and feet lay.</summary>
    public class TrampleTests
    {
        private const float BurnReach = 1.5f;

        private static readonly TrackMarks Tank = new() { Width = 0.6f, Gauge = 2.8f, RutDepth = 1f };

        private ImpactMark _mark;

        [SetUp]
        public void SetUp()
        {
            _mark = ScriptableObject.CreateInstance<ImpactMark>();
            _mark.scar.burnReach = BurnReach;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_mark);

        [TestCase(500f, 301f, 2500, 1505)]
        [TestCase(4000f, 100f, TrampleSurfaces.MaxTextureSize, 500)]
        public void TextureSize_CoversTheMapAtTheTexelSize_CappingHugeMaps(float x, float z, int width, int height) =>
            Assert.AreEqual(new int2(width, height), TrampleSurfaces.TextureSize(new float2(x, z), 0.2f));

        [Test]
        public void FadeSteps_EmptyAChannelInItsDuration_CarryingFractions()
        {
            var carry = 0f;
            var steps = 0;
            for (var frame = 0; frame < 60 * 60; frame++)
            {
                steps += TrampleMap.FadeSteps(ref carry, 1f / 60f, TrampleMap.TrampleRecoverySeconds);
            }

            Assert.That(steps, Is.InRange(TrampleBrush.Levels - 1, TrampleBrush.Levels));

            carry = 0f;
            Assert.AreEqual(0, TrampleMap.FadeSteps(ref carry, 0.5f, TrampleMap.TrackFadeSeconds));
            Assert.AreEqual(1, TrampleMap.FadeSteps(ref carry, 0.5f, TrampleMap.TrackFadeSeconds));
            Assert.AreEqual(0f, carry, 0.07f);
        }

        [Test]
        public void AddVehicle_PutsARutUnderEachTrack_AndFlattensItsWholeWidth()
        {
            var stamps = new TrampleStamps();

            stamps.AddVehicle(Tank, new float2(10f, 20f), new float2(0f, 1f), 0f);
            var trample = stamps.Trample;
            var tracks = stamps.Tracks;

            Assert.AreEqual(2, tracks.Count);
            Assert.AreEqual(8.6f, tracks[0].Center.x, 1e-5f);
            Assert.AreEqual(11.4f, tracks[1].Center.x, 1e-5f);
            MathAssert.Near(new float2(0.3f, 0.3f), tracks[0].HalfSize);
            Assert.AreEqual(TrampleStamps.RutLoneShare, tracks[0].Value.x, 1e-6f);
            Assert.AreEqual(1, trample.Count);
            Assert.AreEqual(1.7f, trample[0].HalfSize.x, 1e-5f);
            Assert.AreEqual(new float4(1f, 1f, 1f, 0f), trample[0].Value);
        }

        [TestCase(2f, 9f, TestName = "Driving forward reaches back")]
        [TestCase(-2f, 11f, TestName = "Reversing reaches forward")]
        public void AddVehicle_CoversTheDistanceDriven(float driven, float center)
        {
            var stamps = new TrampleStamps();

            stamps.AddVehicle(Tank, new float2(0f, 10f), new float2(0f, 1f), driven);

            Assert.AreEqual(center, stamps.Tracks[0].Center.y, 1e-5f);
            Assert.AreEqual(1.3f, stamps.Tracks[0].HalfSize.y, 1e-5f);
        }

        // Wheels share a pass between their axles, and a stamp deepens only the ground it is new on.
        [Test]
        public void AddRuts_DeepenOnlyNewGround_AndSplitTheDeepeningBetweenAxles()
        {
            var stamps = new TrampleStamps();

            stamps.AddRuts(Tank, new float2(0f, 10f), new float2(0f, 1f), 0.6f, 1);
            stamps.AddRuts(Tank, new float2(0f, 10f), new float2(0f, 1f), 0.6f, 2);

            var whole = stamps.Tracks[0];
            Assert.AreEqual(TrampleStamps.RutLoneShare * TrampleStamps.RutDeepening, whole.Value.z, 1e-6f);
            Assert.AreEqual(whole.Value.z * 0.5f, stamps.Tracks[2].Value.z, 1e-6f);
            Assert.AreEqual(0.6f / (0.6f + 0.6f), whole.Rim, 1e-6f);
        }

        [Test]
        public void Footsteps_CoverTheStrideBehindTheFeet_AndOnlyBendPlants()
        {
            var stamps = new TrampleStamps();
            stamps.AddFootsteps(new float2(10f, 0f), new float2(1f, 0f), 0.4f);
            var stamp = stamps.Trample[0];

            Assert.AreEqual(9.8f, stamp.Center.x, 1e-5f);
            Assert.AreEqual(0.3f + 0.2f, stamp.HalfSize.y, 1e-5f);
            Assert.Less(stamp.Value.x, 1f, "leans plants without flattening them");
        }

        [Test]
        public void AddScar_BowlIsHalfTheMarkInsideAWiderScorchDisc()
        {
            _mark.scar.craterDepth = 0.8f;
            _mark.scar.scorch = 0.6f;
            var stamps = new TrampleStamps();

            stamps.AddScar(new float2(1f, 2f), _mark.scar, 4f * 0.5f);

            var scar = stamps.Scars[0];
            Assert.AreEqual(new float2(2f * BurnReach), scar.HalfSize);
            Assert.AreEqual(0.8f / TrampleStamps.GroundRange, scar.Value.x, 1e-5f);
            Assert.AreEqual(0.6f, scar.Value.y, 1e-5f);
            Assert.AreEqual(1f / BurnReach, scar.Value.z, 1e-5f, "bowl share of the stamp");
            Assert.AreEqual(scar.HalfSize, stamps.Trample[0].HalfSize, "plants flattened over the whole scar");
            Assert.AreEqual(1f, stamps.Trample[0].Value.w, "round");
            Assert.IsEmpty(stamps.Tracks, "scars stay out of the fading channels' stamps");
        }

        [Test]
        public void AddScar_WithoutScorch_IsJustTheBowl_AndDeepCratersClip()
        {
            _mark.scar.craterDepth = 9f;
            var stamps = new TrampleStamps();

            stamps.AddScar(float2.zero, _mark.scar, 6f * 0.5f);

            Assert.AreEqual(new float2(3f), stamps.Scars[0].HalfSize);
            Assert.AreEqual(new float4(TrampleStamps.MaxDepth / TrampleStamps.GroundRange, 0f, 1f, 1f), stamps.Scars[0].Value);
            Assert.AreEqual(0f, stamps.Scars[0].Rim);
        }

        [Test]
        public void AddScar_RimStandsAboveTheGround_AndNeedsRoomOutsideTheBowl()
        {
            _mark.scar.craterDepth = 1f;
            _mark.scar.rimHeight = 3f;
            var stamps = new TrampleStamps();

            stamps.AddScar(float2.zero, _mark.scar, 2f);

            var scar = stamps.Scars[0];
            Assert.AreEqual(TrampleStamps.MaxRim / TrampleStamps.GroundRange, scar.Rim, 1e-5f, "the rim clips at its top");
            Assert.AreEqual(2f * TrampleStamps.RimReach, scar.HalfSize.x, 1e-5f, "the stamp has room for the lip");
            Assert.GreaterOrEqual(TrampleStamps.GroundLevelBias - scar.Rim, 0f, "the tallest rim fits under the bias");
        }

        [Test]
        public void AddScar_ScorchWithoutABurnReach_FailsLoudly()
        {
            _mark.scar.scorch = 0.5f;
            _mark.scar.burnReach = 1f;

            Assert.Throws<System.ArgumentException>(() => new TrampleStamps().AddScar(float2.zero, _mark.scar, 2f));
        }

        [Test]
        public void AddFill_SurvivesTheFrameClear_UntilTheBrushDrawsIt()
        {
            var stamps = new TrampleStamps();

            stamps.AddFill(new TurnedRect(new float2(5f, 6f), new float2(1f, 0f), new float2(8f, 4f)), 2.5f);
            stamps.Clear();

            var fill = stamps.Fills[0];
            Assert.AreEqual(new float2(6.5f, 4.5f), fill.HalfSize);
            Assert.AreEqual(new float4(4f, 2f, 2.5f, 0f), fill.Value);
        }

        [Test]
        public void AddScar_StainsCoverTheScorchReach_AndStainsAloneLeaveNoScar()
        {
            _mark.scar.ash = 0.7f;
            _mark.scar.rubble = 0.2f;
            var stamps = new TrampleStamps();

            stamps.AddScar(new float2(1f, 2f), _mark.scar, 4f * 0.5f);

            Assert.AreEqual(1, stamps.Stains.Count);
            Assert.AreEqual(new float2(2f * BurnReach), stamps.Stains[0].HalfSize);
            Assert.AreEqual(new float4(0.2f, 0.7f, 0f, 0f), stamps.Stains[0].Value);
            Assert.IsEmpty(stamps.Scars, "no bowl or scorch, so the soil is not churned");
            Assert.IsEmpty(stamps.Trample);
        }

        [Test]
        public void AddStain_IsARoundDisc_ClampedToFullCover()
        {
            var stamps = new TrampleStamps();

            stamps.AddStain(new float2(3f, 4f), 2.5f, new float4(0f, 0.5f, 2f, -1f));

            Assert.AreEqual(new float2(2.5f), stamps.Stains[0].HalfSize);
            Assert.AreEqual(new float4(0f, 0.5f, 1f, 0f), stamps.Stains[0].Value);
        }

        [Test]
        public void AddFootprintScar_CoversTheFootprint_ScorchedAndChurnedWithoutABowl()
        {
            var stamps = new TrampleStamps();

            stamps.AddFootprintScar(new float2(1f, 2f), new float2(1f, 0f), new float2(8f, 6f), 0.5f);

            var scar = stamps.Scars[0];
            Assert.AreEqual(new float2(4f, 3f), scar.HalfSize);
            Assert.AreEqual(new float2(1f, 0f), scar.Forward);
            Assert.AreEqual(new float4(0f, 0.5f, 0f, 0.5f), scar.Value, "no bowl marks the square shape");
            Assert.IsEmpty(stamps.Trample);
        }

        [Test]
        public void AddWake_TrailsBehindTheUnit_FoamGrowingWithSpeed()
        {
            var stamps = new TrampleStamps();

            stamps.AddWake(new float2(10f, 0f), new float2(1f, 0f), 4f, 2f);
            stamps.AddWake(float2.zero, new float2(0f, 1f), 2f * TrampleStamps.FullWakeSpeed, 2f);

            var wake = stamps.Wakes[0];
            Assert.Less(wake.Center.x, 10f, "centre behind the unit");
            Assert.AreEqual(10f + 2f, wake.Center.x + wake.HalfSize.y, 1e-4f, "front edge at the unit's bow");
            Assert.AreEqual(4f / TrampleStamps.FullWakeSpeed, wake.Value.x, 1e-5f);
            Assert.AreEqual(1f, stamps.Wakes[1].Value.x, "full foam caps");
        }
    }
}
