using HyperRTS.Presentation.Common;
using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Root of an effect rig prefab: child VFX Graphs played by hand from a pool. Bursts send each graph its Burst event
    /// at a pose; streams play their own copies of the stream graphs along a path. Author it +Y up, +Z forward.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Effect Rig")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public partial class EffectRig : PresentationBehaviour
    {
        [Tooltip("Not played beyond this many metres from the camera.")]
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

        /// <summary>Finds the rig's graphs and lights now, as the pool first loads it.</summary>
        public void Prepare() => Capture();

        /// <summary>A random burst scale grown by an impact's splash radius.</summary>
        public float ScaleFor(float splashRadius) =>
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

        private static float Fraction(uint key, uint salt) => (math.hash(new uint2(key, salt)) >> 8) / 16777216f;

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
            if (Lights == null || EffectDetail.Level != DetailLevel.High)
            {
                return;
            }

            foreach (var light in _lights)
            {
                Lights.Flash(light, light.transform.position);
            }
        }

        private void Capture()
        {
            if (_graphs != null)
            {
                return;
            }

            _lights = GetComponentsInChildren<EffectLight>(true);
            var effects = GetComponentsInChildren<VisualEffect>(true);
            _graphs = Array.ConvertAll(effects, effect => new EffectGraph(effect));
            _streams = new GraphStreamPool(effects);
        }
    }
}
