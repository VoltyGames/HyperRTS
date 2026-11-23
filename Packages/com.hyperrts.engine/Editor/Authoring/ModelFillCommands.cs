using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Runs every <see cref="IModelFill"/> on a component, or on every component of the selected prefabs.</summary>
    public static class ModelFillCommands
    {
        public const string Label = "Fill from model";
        private const string Menu = "Assets/HyperRTS/Fill From Model";

        [NoAutoStaticsCleanup]
        private static readonly IModelFill[] Fills = TypeDiscovery.Instances<IModelFill>();

        public static bool CanFill(Type type) => Fills.Any(fill => fill.AppliesTo(type));

        /// <summary>Throws <see cref="InvalidOperationException"/> naming what the model lacks.</summary>
        public static void Fill(Component component)
        {
            foreach (var fill in Fills.Where(fill => fill.AppliesTo(component.GetType())))
            {
                fill.Fill(component);
            }
        }

        [MenuItem(Menu)]
        private static void FillSelected()
        {
            foreach (var path in SelectedPrefabs())
            {
                FillPrefab(path);
            }
        }

        [MenuItem(Menu, true)]
        private static bool CanFillSelected() => SelectedPrefabs().Any();

        private static IEnumerable<string> SelectedPrefabs() =>
            Selection.GetFiltered<GameObject>(SelectionMode.Assets).Select(AssetDatabase.GetAssetPath)
                .Where(path => path.EndsWith(".prefab", StringComparison.Ordinal));

        // A prefab whose model lacks a part stays unsaved, and the console names it; the others still fill.
        private static void FillPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var filled = root.GetComponentsInChildren<Component>(true)
                    .Where(component => component != null && CanFill(component.GetType())).ToList();
                filled.ForEach(Fill);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            catch (InvalidOperationException problem)
            {
                Debug.LogError($"HyperRTS: fill from model on '{path}': {problem.Message}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
