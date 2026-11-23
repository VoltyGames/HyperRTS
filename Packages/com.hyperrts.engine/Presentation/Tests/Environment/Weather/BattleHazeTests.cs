using NUnit.Framework;
using HyperRTS.Presentation.Environment.Weather;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests.Environment.Weather
{
    /// <summary>Battle haze: it halves every 90 s, drifts downwind, lines up with the fog's world-zero texture, and keeps mist low in hollows.</summary>
    public class BattleHazeTests
    {
        private const float Step = 0.25f;
        private static readonly float2 Middle = new(256f, 256f);

        private static readonly FogShape Fog = new(0.0012f, 0f, 17f);
        private BattleHaze _haze;

        [SetUp]
        public void SetUp() => _haze = new BattleHaze(float2.zero, 512f, 0f, Fog);

        [TearDown]
        public void TearDown() => _haze.Dispose();

        [Test]
        public void StillAir_HalvesTheHazeInNinetySeconds()
        {
            _haze.Add(Middle, 30f, 2f);
            var start = _haze.Total();

            Run(BattleHaze.HalfLife, float2.zero);

            Assert.AreEqual(start * 0.5f, _haze.Total(), start * 0.01f);
        }

        [Test]
        public void Wind_CarriesTheHazeDownwind()
        {
            _haze.Add(Middle, 12f, 2f);

            Run(10f, new float2(4f, 0f));

            var downwind = _haze.Amount(Middle + new float2(40f, 0f));
            Assert.Greater(downwind, _haze.Amount(Middle), "the cloud moved 40 m east");
            Assert.Greater(downwind, _haze.Amount(Middle + new float2(0f, 40f)), "and not north");
        }

        [Test]
        public void Texels_ShiftACentredMapToWorldZero()
        {
            var centred = new BattleHaze(new float2(-256f, -256f), 512f, 0f, Fog);
            centred.Add(new float2(2f, 2f), 1f, 1f);
            centred.Advance(0f, float2.zero);

            // The fog reads texel 0 at world 0 to 4 m, so the haze there lands in the first texel of both layers.
            Assert.AreEqual(2f, (float)centred.Texels[0], 0.01f);
            Assert.AreEqual(2f, (float)centred.Texels[BattleHaze.Cells], 0.01f);
            Assert.AreEqual(1f, (float)centred.Texels[1], 0.01f, "clear air keeps the weather's own fog");
            centred.Dispose();
        }

        [Test]
        public void Mist_LiesLowInAHollowAndNotOnARidge()
        {
            // A 64 m wide trough along z, 15 m below the ground either side.
            const int samples = 65;
            var heights = new NativeArray<float>(samples * samples, Allocator.Temp);
            for (var i = 0; i < heights.Length; i++)
            {
                heights[i] = math.abs(i % samples * 8f - 256f) < 32f ? 0f : 15f;
            }

            var terrain = TerrainHeight.Create(heights, new int2(samples, samples), float2.zero, 8f, Allocator.Temp);
            var mist = new BattleHaze(float2.zero, 512f, 0.5f, Fog);
            mist.LayMist(terrain, -100f);
            var ridge = new float2(60f, 256f);

            Assert.Greater(mist.Mist(Middle, 1f), mist.Mist(ridge, 16f), "the trough holds mist, the flat does not");
            Assert.AreEqual(0f, mist.Mist(Middle, 20f), "above its depth the air is clear");

            var cell = BattleHaze.Cells / 2;
            var row = (cell * mist.Layers) * BattleHaze.Cells + cell;
            var lowest = (float)mist.Texels[row];
            // The layer's middle is 2 m up: a third of the mist has faded, and the fog thins by its own height falloff.
            var expected = 1f + 0.5f * BattleHaze.MistMost * (1f - 2f / BattleHaze.MistDepth) / (Fog.Density * Fog.Multiplier(2f));
            Assert.AreEqual(expected, lowest, expected * 0.02f, "mist holds its own extinction whatever the fog's density");
            Assert.AreEqual(1f, (float)mist.Texels[row + mist.Layers / 4 * BattleHaze.Cells], 0.01f, "the layers high over it stay clear");
            mist.Dispose();
            terrain.Blob.Dispose();
            heights.Dispose();
        }

        private void Run(float seconds, float2 drift)
        {
            for (var time = 0f; time < seconds - Step * 0.5f; time += Step)
            {
                _haze.Advance(Step, drift);
            }
        }
    }
}
