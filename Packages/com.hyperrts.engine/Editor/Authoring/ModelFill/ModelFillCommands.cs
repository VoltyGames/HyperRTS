using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Authoring.ModelFill
{
    /// <summary>Runs every <see cref="ModelFill"/> on a component, or on every component under a prefab root.</summary>
    public static class ModelFillCommands
    {
        public const string Label = "Fill from model";

        [NoAutoStaticsCleanup]
        private static readonly ModelFill[] Fills = TypeDiscovery.Instances<ModelFill>();

        public static bool CanFill(Type type) => Fills.Any(fill => fill.AppliesTo(type));

        /// <summary>Throws <see cref="InvalidOperationException"/> naming what the model lacks.</summary>
        public static void Fill(Component component)
        {
            foreach (var fill in Fills.Where(fill => fill.AppliesTo(component.GetType())))
            {
                fill.Fill(component);
            }
        }

        /// <summary>
        /// Fills every component under <paramref name="root"/> that takes values from its model, each even when another
        /// fails; returns why each failed one did.
        /// </summary>
        public static List<string> FillAll(GameObject root)
        {
            var failures = new List<string>();
            var filled = root.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && CanFill(component.GetType())).ToList();
            foreach (var component in filled)
            {
                try
                {
                    Fill(component);
                }
                catch (InvalidOperationException failure)
                {
                    failures.Add(failure.Message);
                }
            }

            return failures;
        }
    }
}
