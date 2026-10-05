using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.NetCode;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Runs every check over the project's prefabs and the open scenes.</summary>
    public static class ProjectValidator
    {
        public static List<ValidationIssue> Run()
        {
            var issues = DuplicateNames(EditorAssets.EntityPrefabs());
            foreach (var prefab in EditorAssets.HyperRTSPrefabs())
            {
                CheckComponents(prefab, issues);
            }

            issues.AddRange(OpenScenes());
            return issues;
        }

        /// <summary>Checks only the open scenes: fast enough to run before entering Play mode.</summary>
        public static List<ValidationIssue> OpenScenes()
        {
            var issues = new List<ValidationIssue>();
            var matches = new List<MatchAuthoring>();
            var entities = new List<GameEntityAuthoring>();
            var anySubScene = false;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                anySubScene |= scene.isSubScene;
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    CheckComponents(root, issues);
                    matches.AddRange(root.GetComponentsInChildren<MatchAuthoring>(true));
                    entities.AddRange(root.GetComponentsInChildren<GameEntityAuthoring>(true));
                }

                CheckNetcodeBootstrap(scene, roots, issues);
            }

            CheckMatchCount(matches, anySubScene, issues);
            if (matches.Count == 1)
            {
                CheckPlacement(matches[0], entities, issues);
            }

            return issues;
        }

        /// <summary>Display names hash to the type id, so two prefabs sharing one merge into one type.</summary>
        public static List<ValidationIssue> DuplicateNames(IEnumerable<GameEntityAuthoring> prefabs)
        {
            var issues = new List<ValidationIssue>();
            foreach (var group in prefabs.GroupBy(prefab => prefab.TypeId).Where(group => group.Count() > 1))
            {
                var names = string.Join(", ", group.Select(prefab => prefab.gameObject.name));
                issues.Add(new ValidationIssue(MessageType.Error,
                    $"Prefabs {names} share the display name '{group.First().DisplayName}'; give each a unique name.",
                    group.First()));
            }

            return issues;
        }

        private static void CheckComponents(GameObject root, List<ValidationIssue> issues)
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

        private static void CheckMatchCount(List<MatchAuthoring> matches, bool anySubScene, List<ValidationIssue> issues)
        {
            if (matches.Count > 1)
            {
                issues.Add(new ValidationIssue(MessageType.Error, "More than one Match in the open scenes; keep exactly one.",
                    matches[1]));
            }
            else if (matches.Count == 0 && anySubScene)
            {
                issues.Add(new ValidationIssue(MessageType.Error, "No Match in the open SubScenes; add GameObject ▸ HyperRTS ▸ Match.",
                    null));
            }
        }

        private static void CheckPlacement(MatchAuthoring match, List<GameEntityAuthoring> entities, List<ValidationIssue> issues)
        {
            var bounds = match.MapRect;
            foreach (var entity in entities)
            {
                if (entity.owner > match.players.Count)
                {
                    issues.Add(new ValidationIssue(MessageType.Warning,
                        $"Owner {entity.owner} has no player slot (the match has {match.players.Count}).", entity));
                }

                var position = entity.transform.position;
                if (!bounds.Contains(new Vector2(position.x, position.z)))
                {
                    issues.Add(new ValidationIssue(MessageType.Warning, "Outside the match's map bounds.", entity));
                }
            }
        }

        private static void CheckNetcodeBootstrap(Scene scene, GameObject[] roots, List<ValidationIssue> issues)
        {
            var subSceneRoot = roots.FirstOrDefault(root => root.GetComponentInChildren<SubScene>(true) != null);
            if (scene.isSubScene || subSceneRoot == null ||
                roots.Any(root => root.GetComponentInChildren<OverrideAutomaticNetcodeBootstrap>(true) != null))
            {
                return;
            }

            issues.Add(new ValidationIssue(MessageType.Warning,
                "No OverrideAutomaticNetcodeBootstrap: Netcode will replace the world. Add the RTS World rig.", subSceneRoot));
        }
    }
}
