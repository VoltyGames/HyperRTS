using System;
using System.Linq;
using HyperRTS.Editor.PlayMode.Staging;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.PlayMode.Staging
{
    /// <summary>Reading a staging file, which needs no Play.</summary>
    public class StagingFileTests
    {
        private const string Good = @"{
            ""scene"": ""Assets/X.unity"",
            ""steps"": [
                { ""at"": 0, ""fog"": false },
                { ""at"": 2, ""order"": { ""units"": ""blue"", ""attack"": ""red"" } },
                { ""at"": 0, ""spawn"": { ""as"": ""blue"", ""prefab"": ""A"", ""owner"": 1, ""count"": 2, ""at"": [1, 2], ""spacing"": 8, ""yaw"": 90 } },
                { ""at"": 0, ""spawn"": { ""as"": ""red"", ""prefab"": ""B"", ""owner"": 2, ""count"": 1, ""at"": [5, 2], ""yaw"": 270 } },
                { ""at"": 1, ""camera"": { ""on"": ""blue"", ""height"": 15, ""follow"": true } },
                { ""at"": 3, ""shot"": ""after"" }
            ]
        }";

        private static string WithSteps(string steps) =>
            Good.Replace("{ \"at\": 0, \"fog\": false },", $"{{ \"at\": 0, \"fog\": false }}, {steps},");

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
        public void ParseRejectsAnOwnerPastAByte()
        {
            var json = Good.Replace("\"owner\": 2", "\"owner\": 300");

            var error = Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(json, "owner.json"));

            StringAssert.Contains("'owner' of 300", error.Message);
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
            var muted = WithSteps("{ \"at\": 0, \"mixer\": { \"Music\": -80, \"Voice\": -6 } }");
            var empty = WithSteps("{ \"at\": 0, \"mixer\": { } }");

            Assert.That(StagingFile.Parse(muted, "muted.json").Steps.Select(step => step.Kind), Has.Member("mixer"));
            StringAssert.Contains("mixer", Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(empty, "empty.json")).Message);
        }

        [Test]
        public void ParseReadsDetailAndRejectsAnEmptyOrNonPositiveOne()
        {
            string With(string detail) => WithSteps($"{{ \"at\": 0, \"detail\": {detail} }}");

            Assert.That(StagingFile.Parse(With("{ \"shadows\": 500, \"lod\": 4 }"), "far.json").Steps.Select(step => step.Kind), Has.Member("detail"));
            StringAssert.Contains("detail", Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(With("{ }"), "empty.json")).Message);
            StringAssert.Contains("'grass'", Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(With("{ \"grass\": 0 }"), "zero.json")).Message);
        }

        [Test]
        public void ParseReadsAQualityLevelByNameAndRejectsAnUnknownOne()
        {
            string With(string level) => WithSteps($"{{ \"at\": 0, \"quality\": \"{level}\" }}");

            Assert.That(StagingFile.Parse(With(QualitySettings.names[0]), "tier.json").Steps.Select(step => step.Kind), Has.Member("quality"));
            StringAssert.Contains("'Potato'", Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(With("Potato"), "tier.json")).Message);
        }

        [Test]
        public void ParseReadsSelectAndWeatherAndRejectsSelectingAnUndefinedGroup()
        {
            string With(string select) => WithSteps($"{{ \"at\": 1, \"select\": \"{select}\" }}, {{ \"at\": 1, \"weather\": \"Rain\" }}");

            Assert.That(StagingFile.Parse(With("blue"), "pick.json").Steps.Select(step => step.Kind), Is.SupersetOf(new[] { "select", "weather" }));
            StringAssert.Contains("group 'green'", Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(With("green"), "pick.json")).Message);
        }

        [Test]
        public void ParseReadsGroupAndLetsLaterStepsUseItsLabelAndRejectsAMissingAsOrRadius()
        {
            string With(string group) => WithSteps($"{{ \"at\": 0, \"group\": {group} }}, {{ \"at\": 1, \"damage\": {{ \"units\": \"barn\", \"amount\": 50 }} }}");

            var file = StagingFile.Parse(With("{ \"as\": \"barn\", \"at\": [10, 20], \"radius\": 15, \"prefab\": \"Barn\" }"), "group.json");

            Assert.That(file.Steps.Select(step => step.Defines), Does.Contain("barn"));
            StringAssert.Contains("'as'", Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(With("{ \"at\": [10, 20], \"radius\": 15 }"), "as.json")).Message);
            StringAssert.Contains("'radius'", Assert.Throws<InvalidOperationException>(() => StagingFile.Parse(With("{ \"as\": \"barn\", \"at\": [10, 20] }"), "radius.json")).Message);
        }
    }
}
