using HyperRTS.Presentation.Environment;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests.Environment
{
    /// <summary>Battle haze: it halves every 90 s, drifts downwind and lines up with the fog's world-zero texture.</summary>
    public class BattleHazeTests
    {
        private const float Step = 0.25f;
        private static readonly float2 Middle = new(256f, 256f);

        private BattleHaze _haze;

        [SetUp]
        public void SetUp() => _haze = new BattleHaze(float2.zero, 512f);

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
            var centred = new BattleHaze(new float2(-256f, -256f), 512f);
            centred.Add(new float2(2f, 2f), 1f, 1f);
            centred.Advance(0f, float2.zero);

            // The fog reads texel 0 at world 0 to 4 m, so the haze there lands in the first texel of both layers.
            Assert.AreEqual(2f, (float)centred.Texels[0], 0.01f);
            Assert.AreEqual(2f, (float)centred.Texels[BattleHaze.Cells], 0.01f);
            Assert.AreEqual(1f, (float)centred.Texels[1], 0.01f, "clear air keeps the weather's own fog");
            centred.Dispose();
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
