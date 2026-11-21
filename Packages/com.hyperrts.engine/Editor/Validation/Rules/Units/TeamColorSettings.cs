using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>The project's team colour bar for the validation rules; create one asset to turn the marker count on.</summary>
    [CreateAssetMenu(menuName = "HyperRTS/Team Colour Settings")]
    public sealed class TeamColorSettings : ScriptableObject
    {
        [Tooltip("Fewest team markers (Team Marker components) a vehicle or building prefab carries, so its owner shows on the model.")]
        [Min(0)]
        public int minMarkers = 2;

        /// <summary>The project asset's bar; false when the project has no asset, which leaves the count off.</summary>
        public static bool TryGetMinMarkers(out int markers)
        {
            markers = 0;
            var guids = AssetDatabase.FindAssets("t:" + nameof(TeamColorSettings));
            if (guids.Length == 0)
            {
                return false;
            }

            if (guids.Length > 1)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: {guids.Length} {nameof(TeamColorSettings)} assets exist; keep one: " +
                    string.Join(", ", guids.Select(AssetDatabase.GUIDToAssetPath)));
            }

            markers = AssetDatabase.LoadAssetAtPath<TeamColorSettings>(AssetDatabase.GUIDToAssetPath(guids[0])).minMarkers;
            return true;
        }
    }
}
