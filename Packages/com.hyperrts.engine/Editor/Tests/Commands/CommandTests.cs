using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Editor.Validation.Issues;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Commands
{
    /// <summary>The <c>validate</c> command reports a broken object with its fix.</summary>
    public class CommandTests : TemplateFixture
    {
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
