using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Rules.UI;
using NUnit.Framework;
using UnityEditor;

namespace HyperRTS.Editor.Tests.Validation;

/// <summary>The UI file rule runs over UXML and USS text held in memory.</summary>
public class UIFilesRuleTests
{
    private const string Header = "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:g=\"Game.Ui\">\n";

    private static UIProject Project(Dictionary<string, string> files) => new()
    {
        AssetExists = path => files.ContainsKey(path),
        ElementExists = name => name is "Game.Ui.Known" or "UnityEngine.UIElements.Label",
        AssetAt = _ => null,
    };

    private static ValidationIssues Run(Dictionary<string, string> files)
    {
        var issues = new ValidationIssues();
        new UIFilesRule().Check(files, Project(files), issues);
        return issues;
    }

    [Test]
    public void StyleAndTemplateLinks_ResolveRelativeRootedAndDatabaseForms_AndMissingOnesAreErrorsWithLines()
    {
        var issues = Run(new Dictionary<string, string>
        {
            ["Assets/A/Screen.uxml"] = Header
                + "<Style src=\"Screen.uss\"/>\n"
                + "<ui:Template src=\"../B/Row.uxml\"/>\n"
                + "<Style src=\"/Assets/B/Row.uss\"/>\n"
                + "<Style src=\"project://database/Assets/B/Row.uss?fileID=1&amp;guid=x\"/>\n"
                + "<ui:Style src=\"Gone.uss\"/>\n</ui:UXML>",
            ["Assets/A/Screen.uss"] = "",
            ["Assets/B/Row.uxml"] = "",
            ["Assets/B/Row.uss"] = "",
        });

        var issue = issues.Single();
        Assert.AreEqual(MessageType.Error, issue.Severity);
        StringAssert.Contains("Assets/A/Screen.uxml:6", issue.Message);
        StringAssert.Contains("Gone.uss", issue.Message);
    }

    [Test]
    public void UnstyledClass_IsAWarning_ButCompoundSelectorsAndUnityClassesPass()
    {
        var issues = Run(new Dictionary<string, string>
        {
            ["Assets/A/Screen.uxml"] = Header
                + "<ui:Label class=\"title row unity-label unused\"/>\n</ui:UXML>",
            ["Assets/A/Screen.uss"] = "/* .unused */\n.title { margin: 0.5px; }\n.row:hover.wide { color: red; }",
        });

        var issue = issues.Single();
        Assert.AreEqual(MessageType.Warning, issue.Severity);
        StringAssert.Contains("'unused'", issue.Message);
        StringAssert.Contains("Screen.uxml:2", issue.Message);
    }

    [Test]
    public void ClassNamesInCode_CountOnlyWhereCodeUsesAClass()
    {
        const string code = @"
                row.AddToClassList(""a""); row.RemoveFromClassList(""b""); row.EnableInClassList(""c"", true);
                row.ClassListContains(""d""); row.ToggleInClassList(""e""); root.Q<Label>(className: ""f"");
                root.Q<Label>(""name"", ""g""); public const string CapsClass = ""h""; const string Kind = ""i"";
                OnClick(""credits"", () => { });";

        CollectionAssert.AreEqual(new[] { "a", "b", "c", "d", "e", "f", "g", "h" }, UIProject.ClassNamesIn(code).ToArray());
    }

    [Test]
    public void ClassUsedByCode_NeedsNoStyle()
    {
        var files = new Dictionary<string, string>
        {
            ["Assets/A/Screen.uxml"] = Header + "<ui:Label class=\"marker lonely\"/></ui:UXML>",
        };
        var project = Project(files);
        project.CodeClassNames.Add("marker");
        var issues = new ValidationIssues();

        new UIFilesRule().Check(files, project, issues);

        StringAssert.Contains("'lonely'", issues.Single().Message);
    }

    [Test]
    public void CustomElement_NeedsItsType_ButUnityAndStructuralTagsPass()
    {
        var issues = Run(new Dictionary<string, string>
        {
            ["Assets/A/Screen.uxml"] = Header + "<ui:Label/>\n<g:Known/>\n<g:Moved/>\n</ui:UXML>",
        });

        var issue = issues.Single();
        Assert.AreEqual(MessageType.Error, issue.Severity);
        StringAssert.Contains("Game.Ui.Moved", issue.Message);
        StringAssert.Contains("Screen.uxml:4", issue.Message);
    }

    [Test]
    public void StyleSheetLink_ToAMissingImage_IsAnError()
    {
        var issues = Run(new Dictionary<string, string>
        {
            ["Assets/A/Screen.uss"] = ".a { background-image: url(\"../Art/Gone.png\"); }",
        });

        StringAssert.Contains("Gone.png", issues.Single().Message);
    }

    [Test]
    public void LoadedUnityAndEngineElements_AllResolve()
    {
        var files = new Dictionary<string, string>
        {
            ["Assets/A/Screen.uxml"] = "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:uie=\"UnityEditor.UIElements\""
                + " xmlns:g=\"HyperRTS.Editor.Authoring\""
                + " xmlns:h=\"HyperRTS.Presentation.HUD\">"
                + "<ui:VisualElement/><ui:Label/><ui:Button/><uie:ObjectField/><g:IssueBadge/><h:LocalizedText/><g:Gone/></ui:UXML>",
        };
        var issues = new ValidationIssues();

        new UIFilesRule().Check(files, UIProject.Open(), issues);

        StringAssert.Contains("HyperRTS.Editor.Authoring.Gone", issues.Single().Message);
    }
}
