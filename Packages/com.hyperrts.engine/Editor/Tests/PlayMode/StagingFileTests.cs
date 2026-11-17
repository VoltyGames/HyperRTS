using System;
using System.Linq;
using HyperRTS.Editor.PlayMode.Staging;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Editor.Tests.PlayMode
{
    /// <summary>Reading a staging file and laying out its spawns, which need no Play.</summary>
    public class StagingFileTests
    {
        private const string Good = @"{
            ""name"": ""check"", ""scene"": ""Assets/X.unity"", ""fog"": false,
            ""steps"": [
                { ""at"": 2, ""order"": { ""units"": ""blue"", ""attack"": ""red"" } },
                { ""at"": 0, ""spawn"": { ""as"": ""blue"", ""prefab"": ""A"", ""owner"": 1, ""count"": 2, ""at"": [1, 2], ""spacing"": 8, ""yaw"": 90 } },
                { ""at"": 0, ""spawn"": { ""as"": ""red"", ""prefab"": ""B"", ""owner"": 2, ""count"": 1, ""at"": [5, 2], ""yaw"": 270 } },
                { ""at"": 1, ""camera"": { ""on"": ""blue"", ""height"": 15, ""follow"": true } },
                { ""at"": 3, ""shot"": ""after"" }
            ]
        }";

        [Test]
        public void ParseOrdersStepsByTimeAndKeepsFileOrderAtTheSameTime()
        {
            var file = StagingFile.Parse(Good, "good.json");

            Assert.That(file.Steps.Select(step => step.Kind), Is.EqualTo(new[] { "spawn", "spawn", "camera", "order", "shot" }));
            Assert.That(file.Steps.Select(step => step.Defines).Where(label => label != null), Is.EqualTo(new[] { "blue", "red" }));
            Assert.That(file.Fog, Is.False);
        }

        [Test]
        public void ParseRejectsAnUnknownKeyNamingItsPath()
        {
            var json = Good.Replace("\"spacing\": 8", "\"spacng\": 8, \"spacing\": 8");

            var error = Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(json, "typo.json"));

            StringAssert.Contains("steps[1].spawn", error.Message);
            StringAssert.Contains("spacng", error.Message);
        }

        [Test]
        public void ParseRejectsAnOrderWithTwoActions()
        {
            var json = Good.Replace("\"attack\": \"red\"", "\"attack\": \"red\", \"stop\": true");

            var error = Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(json, "two.json"));

            StringAssert.Contains("'attack', 'stop' together", error.Message);
        }

        [Test]
        public void ParseRejectsAGroupNoEarlierStepMakes()
        {
            var json = Good.Replace("\"on\": \"blue\"", "\"on\": \"green\"");

            var error = Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(json, "group.json"));

            StringAssert.Contains("group 'green'", error.Message);
        }

        [Test]
        public void ParseRejectsAGroupUsedBeforeItsSpawnRuns()
        {
            var json = Good.Replace("{ \"at\": 2, \"order\"", "{ \"at\": 0.5, \"order\"").Replace("\"at\": 0, \"spawn\": { \"as\": \"red\"", "\"at\": 1, \"spawn\": { \"as\": \"red\"");

            Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(json, "late.json"));
        }

        [Test]
        public void SpawnRowStandsAbreastAcrossTheFacingAroundItsCentre()
        {
            var row = new SpawnRow(new float2(100f, 50f), 90f, 8f, 3);

            Assert.That(row.Position(0).x, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(row.Position(0).y, Is.EqualTo(58f).Within(1e-3f));
            Assert.That(row.Position(1).y, Is.EqualTo(50f).Within(1e-3f));
            Assert.That(row.Position(2).y, Is.EqualTo(42f).Within(1e-3f));
        }
    }
}
