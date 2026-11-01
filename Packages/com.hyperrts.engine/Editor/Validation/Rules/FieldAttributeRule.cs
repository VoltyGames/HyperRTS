using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Checks fields marked <c>[PrefabAsset]</c>, <c>[GhostPrefab]</c> and <c>[NoEmptyEntries]</c>, in nested blocks
    /// and lists too, with the Use Prefab, Make Ghost and Remove Empty fixes.
    /// </summary>
    public sealed class FieldAttributeRule : AuthoringRule<Component>
    {
        public override bool AppliesTo(Component component) => !CheckedFields.Find(component.GetType()).IsEmpty;

        protected override void Check(Component component, ValidationIssues issues) =>
            CheckBlock(component, component, CheckedFields.Find(component.GetType()), "", issues);

        // The location prefix names the list entry a nested field sits in, such as "Extra weapons 2: ".
        private static void CheckBlock(Component owner, object block, CheckedFields fields, string location,
            ValidationIssues issues)
        {
            foreach (var field in fields.Checked)
            {
                CheckField(owner, field, field.Field.GetValue(block), location, issues);
            }

            foreach (var (field, inner) in fields.Nested)
            {
                var value = field.GetValue(block);
                if (value is not IList list)
                {
                    if (value != null)
                    {
                        CheckBlock(owner, value, inner, location, issues);
                    }

                    continue;
                }

                var name = ObjectNames.NicifyVariableName(field.Name);
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] != null)
                    {
                        CheckBlock(owner, list[i], inner, $"{location}{name} {i + 1}: ", issues);
                    }
                }
            }
        }

        private static void CheckField(Component owner, CheckedField field, object value, string location,
            ValidationIssues issues)
        {
            var list = value as IList;
            if (field.NoEmptyEntries != null)
            {
                CheckEmptyEntries(owner, field, list, location, issues);
            }

            if (list == null)
            {
                CheckReference(owner, field, value as Object, null, location, issues);
                return;
            }

            foreach (var entry in list)
            {
                CheckReference(owner, field, entry as Object, list, location, issues);
            }
        }

        private static void CheckEmptyEntries(Component owner, CheckedField field, IList list, string location,
            ValidationIssues issues)
        {
            foreach (var entry in list)
            {
                if (!field.IsEmpty(entry))
                {
                    continue;
                }

                issues.Warn(owner, $"{location}The {field.Name} list has {field.EmptyEntries}, which are ignored.", "Remove Empty",
                    () => QuickFixes.RemoveEmpty(owner, list, field.IsEmpty));
                return;
            }
        }

        private static void CheckReference(Component owner, CheckedField field, Object reference, IList list,
            string location, ValidationIssues issues)
        {
            if (reference == null)
            {
                return;
            }

            if (field.PrefabAsset != null)
            {
                CheckPrefabAsset(owner, field.PrefabAsset.Label, reference, list, location, issues);
            }

            if (field.GhostPrefab != null)
            {
                CheckGhost(owner, field, reference, location, issues);
            }
        }

        private static void CheckPrefabAsset(Component owner, string label, Object reference, IList list,
            string location, ValidationIssues issues)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(reference))
            {
                return;
            }

            var message = $"{location}{label} '{reference.name}' is a scene object; reference the prefab asset.";
            var hasSource = PrefabUtility.GetCorrespondingObjectFromSource(reference) != null;
            if (list != null && hasSource)
            {
                issues.Warn(owner, message, "Use Prefab", () => QuickFixes.UsePrefab(owner, list, reference));
                return;
            }

            issues.Warn(owner, message);
        }

        private static void CheckGhost(Component owner, CheckedField field, Object reference, string location,
            ValidationIssues issues)
        {
            if (field.GhostPrefab.OnlyOnGhosts && !IsGhost(owner))
            {
                return;
            }

            var target = reference is Component component ? component.gameObject : (GameObject)reference;
            WarnIfNotGhost(owner, target,
                $"{location}{field.GhostPrefab.Label} '{target.name}' is not a ghost, so clients won't see it.", issues);
        }
    }
}
