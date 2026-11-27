using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Validation;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Rules.Effects;
using HyperRTS.Editor.Validation.Rules.Units;
using HyperRTS.Editor.Validation.Sets;
using NUnit.Framework;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Tests.Validation;

/// <summary>Each discovered rule reports its minimal broken case, so a new rule ships with one.</summary>
public class RuleFiresTests : TemplateFixture
{
    [NoAutoStaticsCleanup]
    private static readonly FrozenDictionary<Type, string> Skipped = new Dictionary<Type, string>()
    {
        [typeof(ContactShadowsRule)] = "Fires only when the project's active URP renderer lacks the features.",
        [typeof(ScreenOverlayRule)] = "Fires only when the project's active URP renderer lacks the feature.",
        [typeof(SharpenRule)] = "Fires only when the project's active URP renderer lacks the feature.",
        [typeof(UnitSilhouetteRule)] = "Fires only on the project's renderer assets and rendering layer names.",
    }.ToFrozenDictionary();

    private Scene _scene;

    [TearDown]
    public void CloseScene()
    {
        if (_scene.IsValid())
        {
            // Drops the scratch scene and its SubScene flag before other tests read the open scenes.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _scene = default;
        }
    }

    [TestCaseSource(nameof(CoveredRules))]
    public void RuleReportsItsBrokenCase(Type ruleType)
    {
        var brokenCase = RuleFiresCases.Rows[ruleType];
        var input = brokenCase.Build(this);

        var messages = Run(Activator.CreateInstance(ruleType), input).Select(issue => issue.Message).ToList();

        Assert.IsTrue(messages.Any(message => message.Contains(brokenCase.Expected)),
            $"{ruleType.Name} should report '{brokenCase.Expected}', reported: [{string.Join(" | ", messages)}]");
    }

    [Test]
    public void EveryEngineRuleHasABrokenCase()
    {
        var missing = EngineRules().Where(rule => !RuleFiresCases.Rows.ContainsKey(rule) && !Skipped.ContainsKey(rule));

        Assert.IsEmpty(missing.Select(rule => rule.Name));
    }

    [Test]
    public void SkippedRulesHaveNoRow() =>
        Assert.IsEmpty(Skipped.Keys.Where(RuleFiresCases.Rows.ContainsKey).Select(rule => rule.Name));

    private static IEnumerable<Type> CoveredRules() => RuleFiresCases.Rows.Keys.OrderBy(rule => rule.Name);

    // The same set ProjectValidator discovers, minus game rules, which ship their cases in the game's tests.
    private static IEnumerable<Type> EngineRules() =>
        new[] { typeof(IAuthoringRule), typeof(ISceneRule), typeof(IPrefabRule) }
            .SelectMany(contract => TypeCache.GetTypesDerivedFrom(contract).ToArray())
            .Where(type => !type.IsAbstract && !type.ContainsGenericParameters)
            .Where(type => type.Assembly == typeof(IAuthoringRule).Assembly)
            .Distinct();

    private static ValidationIssues Run(object rule, object input)
    {
        var issues = new ValidationIssues();
        switch (input)
        {
            case Component component:
                var authoringRule = (IAuthoringRule)rule;
                Assert.IsTrue(authoringRule.AppliesTo(component),
                    $"{rule.GetType().Name} skips {component.GetType().Name}.");
                authoringRule.Check(component, issues);
                break;
            case SceneSet scenes:
                ((ISceneRule)rule).Check(scenes, issues);
                break;
            case PrefabSet prefabs:
                ((IPrefabRule)rule).Check(prefabs, issues);
                break;
            default:
                throw new ArgumentException($"A row built {input}, not a component, scene set or prefab set.");
        }

        return issues;
    }

    /// <summary>A fresh scene holding what <paramref name="furnish"/> creates, as the validator sees it.</summary>
    internal SceneSet InNewScene(bool isSubScene, Action furnish)
    {
        _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        furnish();
        _scene.isSubScene = isSubScene;
        return new SceneSet(new[] { _scene });
    }
}
