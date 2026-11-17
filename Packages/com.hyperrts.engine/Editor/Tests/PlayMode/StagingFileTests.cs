using System;
using System.Linq;
using HyperRTS.Editor.PlayMode.Staging;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace HyperRTS.Editor.Tests.PlayMode
{
    /// <summary>Reading a staging file and laying out its spawns, which need no Play.</summary>
    public class StagingFileTests
    {
        private const string Good = @"{
            ""name"": ""check"", ""scene"": ""Assets/X.unity"",
            ""steps"": [
                { ""at"": 0, ""fog"": false },
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

            Assert.That(file.Steps.Select(step => step.Kind), Is.EqualTo(new[] { "fog", "spawn", "spawn", "camera", "order", "shot" }));
            Assert.That(file.Steps.Select(step => step.Defines).Where(label => label != null), Is.EqualTo(new[] { "blue", "red" }));
        }

        [Test]
        public void ParseRejectsAnUnknownKeyNamingItsPath()
        {
            var json = Good.Replace("\"spacing\": 8", "\"spacng\": 8, \"spacing\": 8");

            var error = Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(json, "typo.json"));

            StringAssert.Contains("steps[2].spawn", error.Message);
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
        public void ParseReadsTheResolutionInWholePixelsOnly()
        {
            var sized = Good.Replace("\"scene\": \"Assets/X.unity\",", "\"scene\": \"Assets/X.unity\", \"resolution\": [1280, 720],");
            var half = sized.Replace("[1280, 720]", "[1280.5, 720]");

            Assert.That(StagingFile.Parse(sized, "sized.json").Resolution, Is.EqualTo(new Vector2Int(1280, 720)));
            Assert.That(StagingFile.Parse(Good, "good.json").Resolution, Is.EqualTo(Vector2Int.zero));
            StringAssert.Contains("resolution", Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(half, "half.json")).Message);
        }

        [Test]
        public void ParseReadsMixerParametersByNameAndRejectsAnEmptyMixer()
        {
            var muted = Good.Replace("{ \"at\": 0, \"fog\": false },", "{ \"at\": 0, \"fog\": false }, { \"at\": 0, \"mixer\": { \"Music\": -80, \"Voice\": -6 } },");
            var empty = Good.Replace("{ \"at\": 0, \"fog\": false },", "{ \"at\": 0, \"fog\": false }, { \"at\": 0, \"mixer\": { } },");

            Assert.That(StagingFile.Parse(muted, "muted.json").Steps.Select(step => step.Kind), Has.Member("mixer"));
            StringAssert.Contains("mixer", Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(empty, "empty.json")).Message);
        }

        [Test]
        public void GlideTurnedTowardTheSunKeepsItsShapeAndAddsTheHeading()
        {
            var aim = new CameraAim(new float2(3f, 4f), 2f, 10f, 8f, 30f).Turned(100f);

            Assert.That(aim.Yaw, Is.EqualTo(130f));
            Assert.That(aim.Distance, Is.EqualTo(10f));
            Assert.That(aim.Look, Is.EqualTo(new float2(3f, 4f)));
        }

        [Test]
        public void GlideEasesFromItsStartAimToItsEndAndHoldsThere()
        {
            var from = new CameraAim(new float2(0f, 0f), 2f, 10f, 0f, 0f);
            var to = new CameraAim(new float2(0f, 0f), 2f, 20f, 0f, 90f);
            var glide = new GlidePath(from, to, 5f, 4f, _ => 1f, null);

            Assert.That(glide.ViewAt(5f).position, Is.EqualTo(new Vector3(0f, 3f, -10f)).Using(Vector3EqualityComparer.Instance));
            Assert.That(glide.ViewAt(7f).position, Is.EqualTo(new Vector3(-15f * math.SQRT2 / 2f, 3f, -15f * math.SQRT2 / 2f)).Using(Vector3EqualityComparer.Instance));
            Assert.That(glide.ViewAt(20f).position, Is.EqualTo(new Vector3(-20f, 3f, 0f)).Using(Vector3EqualityComparer.Instance));
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
