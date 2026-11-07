using System;
using HyperRTS.Editor.Maps.Build;
using NUnit.Framework;

namespace HyperRTS.Editor.Maps.Tests.Build
{
    /// <summary>Commands find a map by name or spec path; the gallery marks a map out of date.</summary>
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

        [Test]
        public void MapIsOutOfDateOnlyWhenTheSpecWasSavedAfterTheBuild()
        {
            var built = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

            Assert.IsTrue(MapPaths.IsOutOfDate(built.AddMinutes(1), built));
            Assert.IsFalse(MapPaths.IsOutOfDate(built.AddMinutes(-1), built));
            Assert.IsFalse(MapPaths.IsOutOfDate(built, null), "A map never built is not out of date, just unbuilt.");
        }
    }
}
