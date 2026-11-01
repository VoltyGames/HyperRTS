using System;
using HyperRTS.Editor.Maps.Build;
using NUnit.Framework;

namespace HyperRTS.Editor.Maps.Tests.Build
{
    /// <summary>Commands find a map by name or spec path.</summary>
    public class MapPathsTests
    {
        private static readonly string[] Specs = { "Assets/Maps/Kalyna/Kalyna.map.json", "Assets/Maps/Duel/Duel.map.json" };

        [TestCase("Kalyna")]
        [TestCase("Assets/Maps/Kalyna/Kalyna.map.json")]
        [TestCase(@"Assets\Maps\Kalyna\Kalyna.map.json")]
        public void MapIsFoundByNameOrPath(string map) => Assert.AreEqual(Specs[0], MapPaths.Find(map, Specs));

        [Test]
        public void UnknownMapIsNamedWithTheKnownOnes()
        {
            var error = Assert.Throws<ArgumentException>(() => MapPaths.Find("Kalina", Specs));

            StringAssert.Contains("'Kalina'", error.Message);
            StringAssert.Contains("Kalyna, Duel", error.Message);
        }
    }
}
