using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Cues;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Effect rigs are pooled and played detached from their owner, so each must be a prefab that passes
    /// <see cref="EffectRig.Problems"/>.
    /// </summary>
    public static class EffectRigCheck
    {
        /// <summary>Errors on <paramref name="owner"/> for each problem of the rig in <paramref name="field"/>.</summary>
        public static void Check(Object owner, GameObject rig, string field, ValidationIssues issues)
        {
            if (rig == null)
            {
                issues.Error(owner, $"{field} has no effect rig.");
                return;
            }

            CheckOptional(owner, rig, field, issues);
        }

        /// <summary>As <see cref="Check"/>, but an empty field is fine.</summary>
        public static void CheckOptional(Object owner, GameObject rig, string field, ValidationIssues issues)
        {
            if (rig == null)
            {
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

        /// <summary>Checks the catch-all rig and every per-material rig; an empty material rig plays nothing there.</summary>
        public static void CheckSurfaceEffects(Object owner, SurfaceEffects effects, string field,
            ValidationIssues issues)
        {
            if (effects == null)
            {
                issues.Error(owner, $"{field} has no surface effects asset.");
                return;
            }

            Check(owner, effects.anyMaterial, $"{field} '{effects.name}' anyMaterial", issues);
            foreach (var entry in effects.materials)
            {
                CheckOptional(owner, entry.rig, $"{field} '{effects.name}' {entry.material} rig", issues);
            }
        }
    }
}
