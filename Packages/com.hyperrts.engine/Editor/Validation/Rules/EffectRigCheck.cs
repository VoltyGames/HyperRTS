using System.Collections.Generic;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Effects;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Effect rigs are pooled and played detached from their owner, so each must be an <see cref="EffectRig"/> prefab
    /// whose particles stay in world space and scale with the rig's transform.
    /// </summary>
    public static class EffectRigCheck
    {
        /// <summary>What stops <paramref name="rig"/> playing as an effect rig; empty when it is fine.</summary>
        public static List<string> Problems(GameObject rig)
        {
            var problems = new List<string>();
            if (!rig.TryGetComponent<EffectRig>(out _))
            {
                problems.Add("its root has no EffectRig");
            }

            foreach (var system in rig.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                if (main.simulationSpace != ParticleSystemSimulationSpace.World)
                {
                    problems.Add($"particle system '{system.name}' does not simulate in World space");
                }

                if (main.scalingMode != ParticleSystemScalingMode.Hierarchy)
                {
                    problems.Add($"particle system '{system.name}' does not use Hierarchy scaling");
                }
            }

            return problems;
        }

        /// <summary>Errors on <paramref name="owner"/> for each problem of the rig in <paramref name="field"/>.</summary>
        public static void Check(Object owner, GameObject rig, string field, ValidationIssues issues)
        {
            if (rig == null)
            {
                issues.Error(owner, $"{field} has no effect rig.");
                return;
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(rig))
            {
                issues.Error(owner, $"{field} '{rig.name}' is not a prefab asset.");
                return;
            }

            foreach (var problem in Problems(rig))
            {
                issues.Error(owner, $"{field} '{rig.name}': {problem}.");
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
                if (entry.rig != null)
                {
                    Check(owner, entry.rig, $"{field} '{effects.name}' {entry.material} rig", issues);
                }
            }
        }
    }
}
