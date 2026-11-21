using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>The project's team colour bar for the validation rules; create one asset to turn the top-view check on.</summary>
    [CreateAssetMenu(menuName = "HyperRTS/Team Colour Settings")]
    public sealed class TeamColorSettings : ScriptableObject
    {
        [Tooltip("Smallest share of a vehicle's top-down area that must show team colour, so owners read apart from the match camera.")]
        [Range(0f, 1f)]
        public float minTopDownShare = 0.2f;

        /// <summary>The project asset's bar; false when the project has no asset, which leaves the check off.</summary>
        public static bool TryGetMinTopDownShare(out float share)
        {
            share = 0f;
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

            share = AssetDatabase.LoadAssetAtPath<TeamColorSettings>(AssetDatabase.GUIDToAssetPath(guids[0])).minTopDownShare;
            return true;
        }
    }
}
