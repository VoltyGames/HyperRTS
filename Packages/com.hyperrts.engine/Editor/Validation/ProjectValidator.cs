using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Validation.Rules;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>
    /// Runs the discovered rules: component rules for one component (inspectors), and component, prefab, asset and
    /// scene rules over the project's prefabs and assets and the open scenes (the validator window, entering Play mode).
    /// </summary>
    public static class ProjectValidator
    {
        [NoAutoStaticsCleanup]
        private static readonly IAuthoringRule[] ComponentRules = TypeDiscovery.Instances<IAuthoringRule>();
        [NoAutoStaticsCleanup]
        private static readonly ISceneRule[] SceneRules = TypeDiscovery.Instances<ISceneRule>();
        [NoAutoStaticsCleanup]
        private static readonly IPrefabRule[] PrefabRules = TypeDiscovery.Instances<IPrefabRule>();
        [NoAutoStaticsCleanup]
        private static readonly IAssetRule[] AssetRules = TypeDiscovery.Instances<IAssetRule>();

        /// <summary>Every component rule that applies to one component.</summary>
        public static List<ValidationIssue> Check(Component component)
        {
            var issues = new ValidationIssues();
            foreach (var rule in ComponentRules)
            {
                if (rule.AppliesTo(component))
                {
                    Guard(rule, () => rule.Check(component, issues), issues);
                }
            }

            return issues;
        }

        public static List<ValidationIssue> Run()
        {
            var prefabs = PrefabSet.InProject();
            var issues = new ValidationIssues();
            foreach (var prefab in prefabs.All)
            {
                CheckComponents(prefab, issues);
            }

            foreach (var rule in PrefabRules)
            {
                Guard(rule, () => rule.Check(prefabs, issues), issues);
            }

            var assets = AssetSet.InProject();
            foreach (var rule in AssetRules)
            {
                Guard(rule, () => rule.Check(assets, issues), issues);
            }

            issues.AddRange(OpenScenes());
            return issues;
        }

        /// <summary>Checks only the open scenes: fast enough to run before entering Play mode.</summary>
        public static List<ValidationIssue> OpenScenes()
        {
            var scenes = SceneSet.Loaded();
            var issues = new ValidationIssues();
            foreach (var root in scenes.Roots)
            {
                CheckComponents(root, issues);
            }

            foreach (var rule in SceneRules)
            {
                Guard(rule, () => rule.Check(scenes, issues), issues);
            }

            return issues;
        }

        // Names the rule on what it reports; one that throws on broken data reports that too, so the rest of the pass still runs.
        private static void Guard(object rule, Action check, ValidationIssues issues)
        {
            var start = issues.Count;
            try
            {
                check();
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or KeyNotFoundException)
            {
                issues.Error(null, $"{rule.GetType().Name} could not finish: {e.Message}");
            }

            issues.StampFrom(start, RuleName(rule));
        }

        // "MissingCostRule" reads as "Missing Cost".
        private static string RuleName(object rule)
        {
            var name = rule.GetType().Name;
            if (name.EndsWith("Rule"))
            {
                name = name[..^4];
            }

            return Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ");
        }

        private static void CheckComponents(GameObject root, ValidationIssues issues)
        {
            if (root == null)
            {
                return;
            }

            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component != null)
                {
                    issues.AddRange(Check(component));
                }
            }
        }
    }
}
