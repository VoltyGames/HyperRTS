using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Views;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>A view's contrast reads flat ground as flat and light and shade as contrast, and the report keeps it.</summary>
    public class MapViewContrastTests
    {
        private const int Width = 64;
        private const int Height = 48;

        [Test]
        public void AFlatView_HasNoSpreadOrLocalContrast()
        {
            var contrast = MapViewContrast.Measure(Fill((x, y) => 128), Width, Height);

            Assert.AreEqual(0f, contrast.LuminanceSpread, 1e-5f);
            Assert.AreEqual(0f, contrast.LocalContrast, 1e-5f);
        }

        [Test]
        public void ShadowPatches_RaiseBothFigures()
        {
            // 8 px blocks of sun and shade, larger than the 4 px cells.
            var contrast = MapViewContrast.Measure(Fill((x, y) => (x / 8 + y / 8) % 2 == 0 ? (byte)200 : (byte)50),
                Width, Height);

            Assert.AreEqual(150f / 255f, contrast.LuminanceSpread, 1e-3f);
            Assert.Greater(contrast.LocalContrast, 0.1f);
        }

        [Test]
        public void ASmoothGradient_SpreadsWithoutLocalContrast()
        {
            var contrast = MapViewContrast.Measure(Fill((x, y) => (byte)(x * 4)), Width, Height);

            Assert.Greater(contrast.LuminanceSpread, 0.8f);
            Assert.Less(contrast.LocalContrast, 0.01f);
        }

        [Test]
        public void TheReport_KeepsViewContrast_ThroughJson()
        {
            var metrics = new MapMetrics { viewLuminanceSpread = 0.42f, viewLocalContrast = 0.031f };

            var json = JsonUtility.ToJson(MapReport.Create(MapFixture.Spec(), new MapIssues(), metrics));
            var report = JsonUtility.FromJson<MapReport>(json);

            Assert.AreEqual(0.42f, report.metrics.viewLuminanceSpread);
            Assert.AreEqual(0.031f, report.metrics.viewLocalContrast);
        }

        private static Color32[] Fill(System.Func<int, int, byte> grey)
        {
            var pixels = new Color32[Width * Height];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var g = grey(x, y);
                    pixels[y * Width + x] = new Color32(g, g, g, 255);
                }
            }

            return pixels;
        }
    }
}
