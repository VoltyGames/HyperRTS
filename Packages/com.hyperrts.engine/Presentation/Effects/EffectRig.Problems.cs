using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    public partial class EffectRig
    {
        /// <summary>What stops <paramref name="prefab"/> playing as an effect rig; empty when it is fine.</summary>
        public static List<string> Problems(GameObject prefab)
        {
            var problems = new List<string>();
            AddProblems(prefab, "", problems);
            if (!prefab.TryGetComponent<EffectRig>(out var rig))
            {
                return problems;
            }

            for (var i = 0; i < rig.variants.Length; i++)
            {
                var variant = rig.variants[i];
                if (variant == null)
                {
                    problems.Add($"variant {i} is empty");
                    continue;
                }

                AddProblems(variant, $"variant '{variant.name}': ", problems);
                if (variant.TryGetComponent<EffectRig>(out var inner) && inner.variants.Length > 0)
                {
                    problems.Add($"variant '{variant.name}' has variants of its own");
                }
            }

            return problems;
        }

        private static void AddProblems(GameObject prefab, string prefix, List<string> problems)
        {
            if (!prefab.TryGetComponent<EffectRig>(out var rig))
            {
                problems.Add(prefix + "its root has no EffectRig");
            }
            else if (rig.maxDistance <= 0f)
            {
                problems.Add(prefix + "maxDistance must be above 0");
            }

            var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            var subEmitters = SubEmitters(systems);
            foreach (var system in systems)
            {
                var main = system.main;
                if (main.simulationSpace != ParticleSystemSimulationSpace.World)
                {
                    problems.Add($"{prefix}particle system '{system.name}' does not simulate in World space");
                }

                if (main.scalingMode != ParticleSystemScalingMode.Hierarchy)
                {
                    problems.Add($"{prefix}particle system '{system.name}' does not use Hierarchy scaling");
                }

                if (subEmitters.Contains(system))
                {
                    continue;
                }

                // Bursts are replayed by hand, so an endless one would never stop.
                var emission = system.emission;
                for (var b = 0; b < emission.burstCount; b++)
                {
                    if (emission.GetBurst(b).cycleCount < 1)
                    {
                        problems.Add($"{prefix}particle system '{system.name}' burst {b} repeats forever");
                    }
                }
            }
        }
    }
}
