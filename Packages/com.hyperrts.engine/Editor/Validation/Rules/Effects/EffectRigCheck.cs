using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Presentation.Effects.Rig;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Effects
{
    /// <summary>
    /// Effect rigs are pooled and played detached from their owner, so each must be a prefab that passes
    /// <see cref="EffectRig.Problems"/>.
    /// </summary>
    public static class EffectRigCheck
    {
        /// <summary>Errors on <paramref name="owner"/> for each problem of the rig in <paramref name="field"/>; an empty field is an error only when <paramref name="required"/>.</summary>
        public static void Check(Object owner, GameObject rig, string field, bool required, ValidationIssues issues)
        {
            if (rig == null)
            {
                if (required)
                {
                    issues.Error(owner, $"{field} has no effect rig.");
                }

                return;
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(rig))
            {
                issues.Error(owner, $"{field} '{rig.name}' is not a prefab asset.");
                return;
            }

            foreach (var problem in EffectRig.Problems(rig))
            {
                issues.Error(owner, $"{field} '{rig.name}': {problem}.");
            }

            CheckVariantsArePrefabs(owner, rig, field, issues);
        }

        private static void CheckVariantsArePrefabs(Object owner, GameObject rig, string field, ValidationIssues issues)
        {
            if (!rig.TryGetComponent<EffectRig>(out var root))
            {
                return;
            }

            foreach (var variant in root.variants)
            {
                if (variant != null && !PrefabUtility.IsPartOfPrefabAsset(variant))
                {
                    issues.Error(owner, $"{field} '{rig.name}' variant '{variant.name}' is not a prefab asset.");
                }
            }
        }
    }
}
