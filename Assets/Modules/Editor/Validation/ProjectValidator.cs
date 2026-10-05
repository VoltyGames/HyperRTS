using System.Collections.Generic;
using HyperRTS.Editor.Validation.Rules;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Runs every component, scene and prefab rule over the project's prefabs and the open scenes.</summary>
    public static class ProjectValidator
    {
        private static readonly ISceneRule[] SceneRules = TypeDiscovery.Instances<ISceneRule>();
        private static readonly IPrefabRule[] PrefabRules = TypeDiscovery.Instances<IPrefabRule>();

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
                rule.Check(prefabs, issues);
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
                rule.Check(scenes, issues);
            }

            return issues;
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
                    issues.AddRange(AuthoringChecks.For(component));
                }
            }
        }
    }
}
