using System;
using System.Linq;
using HyperRTS.Editor.Commands;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Commands
{
    /// <summary>Commands find a map by name or spec path, and <c>validate</c> reports a broken object with its fix.</summary>
    public class CommandTests : TemplateFixture
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
        public void ValidateReportsABrokenSceneObject()
        {
            var node = Track(EntityTemplates.ResourceNode());

            var report = ValidationReport.From(ProjectValidator.OpenScenes());
            var issue = report.issues.FirstOrDefault(i => i.name == node.name && i.message.Contains("no resource type"));

            Assert.IsNotNull(issue, string.Join(" | ", report.issues.Select(i => i.message)));
            Assert.AreEqual("Error", issue.severity);
            Assert.IsNull(issue.asset);
            Assert.GreaterOrEqual(report.errors, 1);
        }
    }
}
