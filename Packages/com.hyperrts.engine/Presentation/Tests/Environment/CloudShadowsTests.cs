using HyperRTS.Presentation.Effects;
using HyperRTS.Presentation.Environment;
using HyperRTS.Presentation.Tests.Effects;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Tests.Environment
{
    /// <summary>The sun's cookie carries a smoke column's shadow under the column, and drops it on Low detail.</summary>
    public class CloudShadowsTests
    {
        private const int Texels = 64;

        // Straight down, so the light's right is world x and its up is world z.
        private static readonly Quaternion Overhead = Quaternion.Euler(90f, 0f, 0f);

        private GameObject _sun;
        private CloudShadows _shadows;
        private EffectRig _smoke;

        [SetUp]
        public void SetUp()
        {
            _sun = new GameObject("Sun");
            _sun.transform.rotation = Overhead;
            _sun.AddComponent<Light>().type = LightType.Directional;
            _sun.AddComponent<UniversalAdditionalLightData>();
            _shadows = _sun.AddComponent<CloudShadows>();
            _shadows.enabled = false;
            _shadows.clouds = Texture2D.whiteTexture;
            _shadows.area = Texels;
            _shadows.resolution = Texels;
            _shadows.enabled = true;
            _smoke = EffectRigKit.Rig("Smoke");
            _smoke.gameObject.AddComponent<SmokeShadow>().opacity = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sun);
            Object.DestroyImmediate(_smoke.gameObject);
            new SmokeShadows().Show(Vector3.zero, DetailLevel.Low);
        }

        [TestCase(DetailLevel.High, true)]
        [TestCase(DetailLevel.Low, false)]
        public void SmokeColumn_ShadesTheCookieUnderIt_FromMediumUp(DetailLevel level, bool shaded)
        {
            var columns = new SmokeShadows();
            columns.Offers.Add(_smoke, new Vector3(16f, 0f, 8f), 1f, 0u, 100f);
            columns.Show(Vector3.zero, level);
            _shadows.Draw(0f);

            var cookie = Read(_shadows.Cookie);
            // The column's foot at light x 16, y 8 lands at uv 0.75, 0.625; its mirror across the middle stays lit.
            var under = cookie.GetPixel(48, 40).r;
            var mirror = cookie.GetPixel(48, 24).r;
            Object.DestroyImmediate(cookie);
            Assert.AreEqual(shaded, under < 0.5f, $"cookie under the column reads {under}");
            Assert.Greater(mirror, 0.9f, "the mirrored texel stays lit");
        }

        private static Texture2D Read(RenderTexture texture)
        {
            var active = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            read.Apply();
            RenderTexture.active = active;
            return read;
        }
    }
}
