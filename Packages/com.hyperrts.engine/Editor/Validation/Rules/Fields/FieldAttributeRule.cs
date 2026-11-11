using System.Collections;
using HyperRTS.Editor.Validation.Fields;
using HyperRTS.Editor.Validation.Issues;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation.Rules.Fields
{
    /// <summary>
    /// Checks fields marked <c>[PrefabAsset]</c>, <c>[GhostPrefab]</c> and <c>[NoEmptyEntries]</c>, in nested blocks
    /// and lists too, with the Use Prefab, Make Ghost and Remove Empty fixes.
    /// </summary>
    public sealed class FieldAttributeRule : AuthoringRule<Component>
    {
        public override bool AppliesTo(Component component) => !CheckedFields.Find(component.GetType()).IsEmpty;

        protected override void Check(Component component, ValidationIssues issues) =>
            CheckBlock(component, CheckedFields.Find(component.GetType()), new Place(component, "", "", issues));

        // The location prefix names the list entry a nested field sits in, such as "Extra weapons 2: ";
        // the path prefix is the same place as a property path, such as "extraWeapons.Array.data[1].".
        private static void CheckBlock(object block, CheckedFields fields, in Place place)
        {
            foreach (var field in fields.Checked)
            {
                CheckField(field, field.Field.GetValue(block), place.Field(field.Field.Name));
            }

            foreach (var (field, inner) in fields.Nested)
            {
                var value = field.GetValue(block);
                if (value is not IList list)
                {
                    if (value != null)
                    {
                        CheckBlock(value, inner, place.Into(place.Location, $"{place.Path}{field.Name}."));
                    }

                    continue;
                }

                var name = ObjectNames.NicifyVariableName(field.Name);
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] != null)
                    {
                        var entry = place.Into($"{place.Location}{name} {i + 1}: ",
                            $"{place.Path}{field.Name}.Array.data[{i}].");
                        CheckBlock(list[i], inner, entry);
                    }
                }
            }
        }

        private static void CheckField(CheckedField field, object value, in Place place)
        {
            var list = value as IList;
            if (field.NoEmptyEntries != null)
            {
                CheckEmptyEntries(field, list, place);
            }

            if (list == null)
            {
                CheckReference(field, value as Object, null, place);
                return;
            }

            foreach (var entry in list)
            {
                CheckReference(field, entry as Object, list, place);
            }
        }

        private static void CheckEmptyEntries(CheckedField field, IList list, in Place place)
        {
            foreach (var entry in list)
            {
                if (!field.IsEmpty(entry))
                {
                    continue;
                }

                var owner = place.Owner;
                place.Issues.Warn(owner,
                    $"{place.Location}The {field.Name} list has {field.EmptyEntries}, which are ignored.",
                    "Remove Empty", () => QuickFixes.RemoveEmpty(owner, list, field.IsEmpty), place.Path);
                return;
            }
        }

        private static void CheckReference(CheckedField field, Object reference, IList list, in Place place)
        {
            if (reference == null)
            {
                return;
            }

            if (field.PrefabAsset != null)
            {
                CheckPrefabAsset(field.PrefabAsset.Label, reference, list, place);
            }

            if (field.GhostPrefab != null)
            {
                CheckGhost(field, reference, place);
            }
        }

        private static void CheckPrefabAsset(string label, Object reference, IList list, in Place place)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(reference))
            {
                return;
            }

            var owner = place.Owner;
            var message = $"{place.Location}{label} '{reference.name}' is a scene object; reference the prefab asset.";
            var hasSource = PrefabUtility.GetCorrespondingObjectFromSource(reference) != null;
            if (list != null && hasSource)
            {
                place.Issues.Warn(owner, message, "Use Prefab", () => QuickFixes.UsePrefab(owner, list, reference),
                    place.Path);
                return;
            }

            place.Issues.Warn(owner, message, path: place.Path);
        }

        private static void CheckGhost(CheckedField field, Object reference, in Place place)
        {
            if (field.GhostPrefab.OnlyOnGhosts && !IsGhost(place.Owner))
            {
                return;
            }

            var target = reference is Component component ? component.gameObject : (GameObject)reference;
            WarnIfNotGhost(place.Owner, target,
                $"{place.Location}{field.GhostPrefab.Label} '{target.name}' is not a ghost, so clients won't see it.",
                place.Issues, place.Path);
        }

        // The location prefix and path prefix of the field being checked, with the owner and where issues go.
        private readonly struct Place
        {
            public readonly Component Owner;
            public readonly string Location;
            public readonly string Path;
            public readonly ValidationIssues Issues;

            public Place(Component owner, string location, string path, ValidationIssues issues)
            {
                Owner = owner;
                Location = location;
                Path = path;
                Issues = issues;
            }

            public Place Into(string location, string path) => new(Owner, location, path, Issues);

            public Place Field(string name) => new(Owner, Location, Path + name, Issues);
        }
    }
}
