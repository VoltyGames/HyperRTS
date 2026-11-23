using System;
using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Sets;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Fields
{
    /// <summary>Reports a component with a fill from model whose values are off from what its model gives.</summary>
    public sealed class ModelFillRule : AuthoringRule<Component>
    {
        private const string FixLabel = "Fill From Model";

        public override bool AppliesTo(Component component) => ModelFillCommands.CanFill(component.GetType());

        protected override void Check(Component component, ValidationIssues issues)
        {
            // A base prefab holds stand-in meshes; its variants carry the models the values come from.
            if (PrefabSet.InProject().IsBase(component.transform.root.gameObject))
            {
                return;
            }

            string path;
            try
            {
                path = new ModelFillCheck(component).FirstDifference();
            }
            catch (InvalidOperationException problem)
            {
                issues.Warn(component, $"Fill from model fails: {problem.Message}");
                return;
            }

            if (path == null)
            {
                return;
            }

            var field = ObjectNames.NicifyVariableName(path.Split('.')[0]);
            issues.Warn(component, $"{field} is off from what the model gives.", FixLabel,
                () => EditorUndo.RecordHierarchy(component.gameObject, FixLabel, () => ModelFillCommands.Fill(component)), path);
        }
    }
}
