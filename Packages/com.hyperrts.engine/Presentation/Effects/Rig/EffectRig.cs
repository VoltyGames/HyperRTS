using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects.Lights;
using HyperRTS.Presentation.Effects.Streams;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

namespace HyperRTS.Presentation.Effects.Rig
{
    /// <summary>
    /// Root of an effect rig prefab: child VFX Graphs played by hand from a pool. Bursts send each graph its Burst event
    /// at a pose; streams play their own copies of the stream graphs along a path. Author it +Y up, +Z forward.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Look + "Effects/Effect Rig")]
    [Icon(HyperRTSIcons.EffectRig)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class EffectRig : PresentationBehaviour
    {
        [Tooltip("Not played beyond this distance from the camera.")]
        [Unit("m")]
        public float maxDistance = 150f;

        [Tooltip("Random scale range of each burst.")]
        public Vector2 scale = Vector2.one;

        [Tooltip("Scale added per metre of an impact's splash radius.")]
        public float scalePerRadius;

        [Tooltip("Impacts face the ground normal instead of straight up.")]
        public bool alignToGround = true;

        [Tooltip("Turns each burst, and each entity's loop, by a random angle about the rig's +Y. Off keeps aim.")]
        public bool randomSpin;

        [Tooltip("Alternative rigs, each without variants of its own. Each play picks this rig or one of them.")]
        public GameObject[] variants = Array.Empty<GameObject>();

        private EffectGraph[] _graphs;
        private GraphStreamPool _streams;
        private EffectLight[] _lights;

        /// <summary>The shared flash lights; none means no flashes.</summary>
        public EffectLights Lights { get; set; }

        /// <summary>Set by the pool from <see cref="variants"/>.</summary>
        public EffectRig[] Alternatives { get; set; } = Array.Empty<EffectRig>();

        /// <summary>The rig's loop light, found when it is first captured; null without one.</summary>
        public LoopLight LoopLight { get; private set; }

        /// <summary>The rig's smoke shadow, found when it is first captured; null without one.</summary>
        public SmokeShadow SmokeShadow { get; private set; }

        /// <summary>Finds the rig's graphs and lights now, as the pool first loads it.</summary>
        public void Capture()
        {
            if (_graphs != null)
            {
                return;
            }

            _lights = GetComponentsInChildren<EffectLight>(true);
            LoopLight = GetComponent<LoopLight>();
            SmokeShadow = GetComponent<SmokeShadow>();
            var effects = GetComponentsInChildren<VisualEffect>(true);
            _graphs = Array.ConvertAll(effects, effect => new EffectGraph(effect));
            _streams = new GraphStreamPool(effects);
        }

        /// <summary>A random burst scale grown by an impact's splash radius.</summary>
        private float ScaleFor(float splashRadius) =>
            UnityEngine.Random.Range(scale.x, scale.y) + scalePerRadius * splashRadius;

        /// <summary>Bursts a random variant at a random scale from its range, grown by an impact's splash radius.</summary>
        public void Play(Vector3 position, Quaternion rotation, float splashRadius = 0f)
        {
            var played = Pick();
            played.Burst(position, rotation, played.ScaleFor(splashRadius), EffectDetail.Density);
        }

        public EffectRig Pick(uint key)
        {
            var pick = (int)(key % (uint)(Alternatives.Length + 1));
            return pick == 0 ? this : Alternatives[pick - 1];
        }

        public EffectRig Pick() => Pick((uint)UnityEngine.Random.Range(0, int.MaxValue));

        public float LoopScale(uint key) => Mathf.Lerp(scale.x, scale.y, Fraction(key, 1u));

        public Quaternion LoopRotation(Quaternion rotation, uint key) =>
            randomSpin ? rotation * Yaw(Fraction(key, 2u)) : rotation;

        private static float Fraction(uint key, uint salt) => Hashes.Fraction(math.hash(new uint2(key, salt)));

        /// <summary>Sends every graph its Burst event at the pose, thinned by density, and flashes the lights.</summary>
        public void Burst(Vector3 position, Quaternion rotation, float burstScale, float density)
        {
            Capture();
            if (randomSpin)
            {
                rotation *= Yaw(UnityEngine.Random.value);
            }

            Pose(position, rotation, burstScale);
            foreach (var graph in _graphs)
            {
                graph.Burst(position, rotation, burstScale, density);
            }

            Flash();
        }

        private static Quaternion Yaw(float fraction) => Quaternion.Euler(0f, fraction * 360f, 0f);

        /// <summary>A stream's started copy of the stream graphs; null when the rig has none.</summary>
        public GraphStream StartStream()
        {
            Capture();
            return _streams.HasStreams ? _streams.Start() : null;
        }

        /// <summary>Stops a stream's graphs, whose particles then live out their lives.</summary>
        public void StopStream(GraphStream stream) => _streams.Stop(stream);

        /// <summary>Sends graph bursts that waited for the graph's first run, and frees stopped streams.</summary>
        public void Tick()
        {
            Capture();
            _streams.Tick();
            foreach (var graph in _graphs)
            {
                graph.Tick();
            }
        }

        // Flash lights are children of the rig, so the pose places them.
        private void Pose(Vector3 position, Quaternion rotation, float poseScale)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = Vector3.one * poseScale;
        }

        private void Flash()
        {
            if (Lights == null || PresentationQuality.Current.Effects != DetailLevel.High)
            {
                return;
            }

            foreach (var light in _lights)
            {
                Lights.Flash(light, light.transform.position);
            }
        }

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

            foreach (var effect in prefab.GetComponentsInChildren<VisualEffect>(true))
            {
                AddGraphProblems(effect, prefix, problems);
            }
        }

        private static void AddGraphProblems(VisualEffect effect, string prefix, List<string> problems)
        {
            if (effect.visualEffectAsset == null)
            {
                problems.Add($"{prefix}VFX graph '{effect.name}' has no asset");
                return;
            }

            // The pool sends Burst itself, so a graph that starts on its own would play once at the origin.
            if (!string.IsNullOrEmpty(effect.initialEventName))
            {
                problems.Add($"{prefix}VFX graph '{effect.name}' plays on its own (clear Initial Event Name)");
            }

            if (!EffectGraph.HasDensity(effect))
            {
                problems.Add($"{prefix}VFX graph '{effect.name}' has no {EffectGraph.DensityProperty} property");
            }
        }
    }
}
