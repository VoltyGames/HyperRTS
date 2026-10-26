using System;
using HyperRTS.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Tests.Environment
{
    /// <summary>Weather: the map's weather set, wet-only decal groups and the scrolling wind gust map.</summary>
    public class WeatherTests
    {
        private GameObject _weather;
        private GameObject _overcast;
        private WeatherSet _set;
        private GameObject _group;
        private GameObject _puddle;

        [SetUp]
        public void SetUp()
        {
            _weather = new GameObject("Weather");
            _overcast = new GameObject("Overcast");
            _set = _weather.AddComponent<WeatherSet>();
            _set.defaultKey = "Clear";
            _set.atmospheres.Add("Clear", new GameObject("Clear"));
            _set.atmospheres.Add("Overcast", _overcast);
            new GameObject("Default").transform.SetParent(_weather.transform);

            Wetness.Publish(0f);
            _group = new GameObject("Wet Decals");
            _puddle = new GameObject("Puddle 1");
            _puddle.transform.SetParent(_group.transform, false);
            _group.AddComponent<WetnessGroup>().threshold = 0.5f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var atmosphere in _set.atmospheres.Values)
            {
                Object.DestroyImmediate(atmosphere);
            }

            Object.DestroyImmediate(_weather);
            Object.DestroyImmediate(_group);
            Wetness.Publish(0f);
        }

        [Test]
        public void Find_ReturnsTheWeathersAtmosphere() => Assert.AreSame(_overcast, _set.Find("Overcast"));

        [Test]
        public void AnUnknownKey_ThrowsNamingIt_AndKeepsTheWeather()
        {
            var error = Assert.Throws<InvalidOperationException>(() => _set.Apply("Blizzard"));

            StringAssert.Contains("'Blizzard'", error.Message);
            Assert.AreEqual(1, _weather.transform.childCount, "the default atmosphere stays");
        }

        [Test]
        public void AnEmptyKey_KeepsTheMapsWeather()
        {
            _set.Apply("");

            Assert.AreEqual("Default", _weather.transform.GetChild(0).name);
        }

        [Test]
        public void Wetness_ShowsChildrenFromTheThreshold_AndHidesThemAgainWhenDry()
        {
            _group.GetComponent<WetnessGroup>().Apply();
            Assert.IsFalse(_puddle.activeSelf, "dry ground");

            Wetness.Publish(0.4f);
            Assert.IsFalse(_puddle.activeSelf);

            Wetness.Publish(0.8f);
            Assert.IsTrue(_puddle.activeSelf);

            Wetness.Publish(0f);
            Assert.IsFalse(_puddle.activeSelf);
        }

        [Test]
        public void DisabledWetnessGroup_StopsListening()
        {
            _group.GetComponent<WetnessGroup>().enabled = false;

            Wetness.Publish(0.8f);

            Assert.IsFalse(_puddle.activeSelf);
        }

        [TestCase(0.1f, 0.2f, 0.6f, 0.8f, 0.5f, 0.5f, 0.25f, 0.4f, TestName = "Moves downwind by tiles a second")]
        [TestCase(0.9f, 0.05f, 1f, -1f, 0.2f, 1f, 0.1f, 0.85f, TestName = "Wraps into one tile")]
        public void Gust_Scroll(float x, float y, float windX, float windY, float speed, float seconds, float expectedX,
            float expectedY)
        {
            var offset = WindGust.Scroll(new Vector2(x, y), new Vector2(windX, windY), speed, seconds);

            Assert.AreEqual(expectedX, offset.x, 1e-5f);
            Assert.AreEqual(expectedY, offset.y, 1e-5f);
        }

        [Test]
        public void GustNoise_TilesAcrossTheEdges()
        {
            for (var step = 0; step <= 10; step++)
            {
                var t = step / 10f;
                Assert.AreEqual(WindGust.Noise(0f, t), WindGust.Noise(1f, t), 1e-5f);
                Assert.AreEqual(WindGust.Noise(t, 0f), WindGust.Noise(t, 1f), 1e-5f);
            }
        }

        [Test]
        public void GustMap_SpansTheFullRange_AndRepeats()
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
