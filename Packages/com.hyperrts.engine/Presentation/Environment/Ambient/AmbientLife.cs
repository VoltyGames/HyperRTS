using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Cues;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>
    /// The scene's ambient life: bird flocks circling over the map and dust devils drifting downwind. Client visuals
    /// only (<see cref="BirdFlockSystem"/>, <see cref="DustDevilSystem"/>); a zero count turns a feature off.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Environment + "Ambient Life")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class AmbientLife : MonoBehaviour
    {
        // Graphics.RenderMeshInstanced draws at most this many instances per call.
        private const int MaxBirds = 1023;

        [Header("Birds")]
        [Tooltip("Bird quad material: GPU Instancing on, double-sided, alpha clip, a 2×2 flipbook of wing poses in " +
                 "_BaseMap (frames left to right, top to bottom; seen from above, head up). Each frame is drawn by " +
                 "setting _BaseMap_ST, so URP Unlit works as is.")]
        public Material birdMaterial;

        [Tooltip("Flocks over the map. 0 turns birds off.")]
        [Min(0)]
        public int flocks = 4;

        [Tooltip("Birds in one flock, min and max.")]
        public Vector2Int birdsPerFlock = new(8, 20);

        [Tooltip("Flying height above the ground in metres, min and max.")]
        public Vector2 height = new(15f, 40f);

        [Tooltip("Width of a bird quad in metres.")]
        [Min(0.01f)]
        public float birdSize = 0.5f;

        [Tooltip("Metres from a flock within which an impact scatters it.")]
        [Min(0f)]
        public float scatterRadius = 60f;

        [Tooltip("Seconds a scattered flock flies off before it settles over a new spot, min and max.")]
        public Vector2 returnDelay = new(8f, 15f);

        [Header("Dust devils")]
        [Tooltip("Effect rig streamed along each dust devil's path.")]
        public GameObject dustDevilRig;

        [Tooltip("Ground materials a dust devil may form on.")]
        public SurfaceMaterial[] dustDevilGround = { SurfaceMaterial.Soil };

        [Tooltip("Seconds between spawn attempts, min and max.")]
        public Vector2 dustDevilInterval = new(20f, 45f);

        [Tooltip("Dust devils alive at once. 0 turns them off.")]
        [Min(0)]
        public int maxDustDevils = 2;

        [Tooltip("Seconds one dust devil lasts, min and max.")]
        public Vector2 dustDevilLifetime = new(12f, 25f);

        [Tooltip("Drift speed downwind in metres per second.")]
        [Min(0f)]
        public float dustDevilSpeed = 3f;

        /// <summary>The enabled instance in the loaded scenes, or null.</summary>
        public static AmbientLife Active { get; private set; }

        public BirdSettings Birds => new()
        {
            Height = new float2(height.x, height.y),
            ScatterRadius = scatterRadius,
            ReturnDelay = new float2(returnDelay.x, returnDelay.y),
        };

        /// <summary>Throws naming the first bird field that can't be used.</summary>
        public void RequireBirds()
        {
            Require(birdMaterial != null, nameof(birdMaterial), "is missing");
            Require(birdMaterial.enableInstancing, nameof(birdMaterial), "needs GPU Instancing enabled");
            Require(birdsPerFlock.x >= 1 && birdsPerFlock.y >= birdsPerFlock.x, nameof(birdsPerFlock), "needs 1 ≤ min ≤ max");
            Require(flocks * birdsPerFlock.y <= MaxBirds, nameof(birdsPerFlock), $"allows over {MaxBirds} birds in all");
            Require(IsRange(height) && height.x > 0f, nameof(height), "needs 0 < min ≤ max");
            Require(IsRange(returnDelay), nameof(returnDelay), "needs 0 ≤ min ≤ max");
        }

        /// <summary>Throws naming the first dust devil field that can't be used.</summary>
        public void RequireDustDevils()
        {
            Require(dustDevilRig != null, nameof(dustDevilRig), "is missing");
            Require(dustDevilGround is { Length: > 0 }, nameof(dustDevilGround), "is empty");
            Require(IsRange(dustDevilInterval) && dustDevilInterval.x > 0f, nameof(dustDevilInterval), "needs 0 < min ≤ max");
            Require(IsRange(dustDevilLifetime) && dustDevilLifetime.x > 0f, nameof(dustDevilLifetime), "needs 0 < min ≤ max");
        }

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private static bool IsRange(Vector2 range) => range.x >= 0f && range.y >= range.x;

        private void Require(bool valid, string field, string problem)
        {
            if (!valid)
            {
                throw new InvalidOperationException($"AmbientLife '{name}': {field} {problem}.");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active = null;
    }
}
