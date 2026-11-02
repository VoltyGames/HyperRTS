using System;
using System.Reflection;
using HyperRTS.Simulation.Common.Authoring;
using UnityEditor;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>One field with its <see cref="PrefabAssetAttribute"/>, <see cref="GhostPrefabAttribute"/> and <see cref="NoEmptyEntriesAttribute"/>.</summary>
    internal sealed class CheckedField
    {
        public CheckedField(FieldInfo field, Type element)
        {
            Field = field;
            Name = Lower(field.Name);
            PrefabAsset = field.GetCustomAttribute<PrefabAssetAttribute>();
            GhostPrefab = field.GetCustomAttribute<GhostPrefabAttribute>();
            NoEmptyEntries = field.GetCustomAttribute<NoEmptyEntriesAttribute>();
            Key = NoEmptyEntries == null ? null : FindKey(field, element, NoEmptyEntries.Key);
            EmptyEntries = Key == null ? "empty entries" : $"entries with no {Lower(Key.Name)}";
        }

        public FieldInfo Field { get; }

        /// <summary>How a warning names the list, e.g. "build options".</summary>
        public string Name { get; }

        /// <summary>How a warning names the entries <see cref="IsEmpty"/> finds, e.g. "entries with no damage type".</summary>
        public string EmptyEntries { get; }

        public PrefabAssetAttribute PrefabAsset { get; }

        public GhostPrefabAttribute GhostPrefab { get; }

        public NoEmptyEntriesAttribute NoEmptyEntries { get; }

        /// <summary>The entry field <see cref="NoEmptyEntries"/> requires, or null when the entry itself must be set.</summary>
        public FieldInfo Key { get; }

        public bool HasChecks => PrefabAsset != null || GhostPrefab != null || NoEmptyEntries != null;

        public bool IsEmpty(object entry) => IsMissing(Key == null ? entry : Key.GetValue(entry));

        public static bool IsMissing(object value) => value is Object unityObject ? unityObject == null : value == null;

        private static string Lower(string fieldName) => ObjectNames.NicifyVariableName(fieldName).ToLowerInvariant();

        private static FieldInfo FindKey(FieldInfo field, Type element, string key)
        {
            if (key == null)
            {
                return typeof(Object).IsAssignableFrom(element) ? null : throw new InvalidOperationException(
                    $"[NoEmptyEntries] on '{field.DeclaringType?.Name}.{field.Name}' needs a Key: its entries are never null.");
            }

            return element.GetField(key) ?? throw new InvalidOperationException(
                $"[NoEmptyEntries] on '{field.DeclaringType?.Name}.{field.Name}' names a key '{key}' that {element.Name} lacks.");
        }
    }
}
