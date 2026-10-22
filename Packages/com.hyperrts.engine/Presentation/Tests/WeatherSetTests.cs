using System;
using HyperRTS.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>A map's weather set finds each weather's atmosphere and refuses keys it doesn't offer.</summary>
    public class WeatherSetTests
    {
        private GameObject _go;
        private GameObject _overcast;
        private WeatherSet _set;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Weather");
            _overcast = new GameObject("Overcast");
            _set = _go.AddComponent<WeatherSet>();
            _set.defaultKey = "Clear";
            _set.atmospheres.Add("Clear", new GameObject("Clear"));
            _set.atmospheres.Add("Overcast", _overcast);
            new GameObject("Default").transform.SetParent(_go.transform);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var atmosphere in _set.atmospheres.Values)
            {
                Object.DestroyImmediate(atmosphere);
            }

            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Find_ReturnsTheWeathersAtmosphere() => Assert.AreSame(_overcast, _set.Find("Overcast"));

        [Test]
        public void AnUnknownKey_ThrowsNamingIt_AndKeepsTheWeather()
        {
            var error = Assert.Throws<InvalidOperationException>(() => _set.Apply("Blizzard"));

            StringAssert.Contains("'Blizzard'", error.Message);
            Assert.AreEqual(1, _go.transform.childCount, "the default atmosphere stays");
        }

        [Test]
        public void AnEmptyKey_KeepsTheMapsWeather()
        {
            _set.Apply("");

            Assert.AreEqual("Default", _go.transform.GetChild(0).name);
        }
    }
}
