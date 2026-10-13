using HyperRTS.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public class WindGustTests
    {
        [Test]
        public void Scroll_MovesDownwindByTilesPerSecond()
        {
            var offset = WindGust.Scroll(new Vector2(0.1f, 0.2f), new Vector2(0.6f, 0.8f), 0.5f, 0.5f);

            Assert.AreEqual(0.25f, offset.x, 1e-5f);
            Assert.AreEqual(0.4f, offset.y, 1e-5f);
        }

        [Test]
        public void Scroll_WrapsIntoOneTile()
        {
            var offset = WindGust.Scroll(new Vector2(0.9f, 0.05f), new Vector2(1f, -1f), 0.2f, 1f);

            Assert.AreEqual(0.1f, offset.x, 1e-5f);
            Assert.AreEqual(0.85f, offset.y, 1e-5f);
        }

        [Test]
        public void Noise_TilesAcrossTheEdges()
        {
            for (var step = 0; step <= 10; step++)
            {
                var t = step / 10f;
                Assert.AreEqual(WindGust.Noise(0f, t), WindGust.Noise(1f, t), 1e-5f);
                Assert.AreEqual(WindGust.Noise(t, 0f), WindGust.Noise(t, 1f), 1e-5f);
            }
        }

        [Test]
        public void CreateMap_SpansTheFullRange()
        {
            var map = WindGust.CreateMap(32);
            try
            {
                var pixels = map.GetPixelData<byte>(0);
                byte low = 255;
                byte high = 0;
                foreach (var pixel in pixels)
                {
                    low = (byte)Mathf.Min(low, pixel);
                    high = (byte)Mathf.Max(high, pixel);
                }

                Assert.AreEqual(0, low);
                Assert.AreEqual(255, high);
                Assert.AreEqual(TextureWrapMode.Repeat, map.wrapMode);
            }
            finally
            {
                Object.DestroyImmediate(map);
            }
        }
    }
}
