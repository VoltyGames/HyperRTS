using System;
using System.Linq;
using System.Numerics;
using HyperRTS.Editor.Environment;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Tools
{
    /// <summary>The editor FFT and the ripple spectrum built on it.</summary>
    public class FftTests
    {
        [Test]
        public void Inverse_OfOneWave_IsThatWave()
        {
            const int size = 16;
            var values = new Complex[size];
            values[3] = Complex.One;
            Fft.Inverse(values);
            for (var n = 0; n < size; n++)
            {
                var angle = 2.0 * Math.PI * 3 * n / size;
                Assert.That(values[n].Real, Is.EqualTo(Math.Cos(angle)).Within(1e-9), $"real at {n}");
                Assert.That(values[n].Imaginary, Is.EqualTo(Math.Sin(angle)).Within(1e-9), $"imaginary at {n}");
            }
        }

        [Test]
        public void Inverse2D_RejectsSizesThatArentPowersOfTwo() =>
            Assert.Throws<ArgumentException>(() => Fft.Inverse2D(new Complex[36], 6));

        [Test]
        public void SpectrumNormals_AreUnit_FaceUp_AndTiltEveryWay()
        {
            var normals = WaterSpectrum.Normals(64, 3u, 7f, 0.4f);
            Assert.That(normals.All(n => Math.Abs(n.magnitude - 1f) < 1e-4f), Is.True);
            Assert.That(normals.All(n => n.z > 0f), Is.True);
            Assert.That(normals.Average(n => n.x), Is.EqualTo(0f).Within(0.02f));
            Assert.That(normals.Average(n => n.y), Is.EqualTo(0f).Within(0.02f));
        }
    }
}
