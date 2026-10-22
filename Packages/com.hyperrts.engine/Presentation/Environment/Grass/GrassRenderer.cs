using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>
    /// Scatters a map's grass and small plants on the GPU from a <see cref="GrassField"/> for each camera about to
    /// render, in edit mode too, and draws them in a shadowed near band and an unshadowed far band. Each camera keeps
    /// its own buffers and rescatters only when its view or the quality settings change.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed partial class GrassRenderer : PresentationBehaviour
    {
        [AutoStaticsCleanup]
        private static float _density = PresentationQuality.Default.GrassDensity;
        [AutoStaticsCleanup]
        private static float _distance = PresentationQuality.Default.GrassDistance;
        [AutoStaticsCleanup]
        private static float _shadowDistance = PresentationQuality.Default.GrassShadowDistance;

        [Tooltip("The baked plants this map grows.")]
        public GrassField field;

        [Tooltip("The Grass compute shader.")]
        public ComputeShader compute;

        [Tooltip("The Hidden/HyperRTS/FoliageGrass shader each plant's material is copied onto.")]
        public Shader shader;

        [Tooltip("The terrain the plants stand on.")]
        public Terrain terrain;

        private readonly CameraStates<GrassCameraState> _cameras = new();
        private Func<GrassCameraState> _newState;
        private GrassComputeInputs _kernels;
        private Material[] _materials;
        private ScatterTiles _tiles;
        private bool _ready;
#if UNITY_EDITOR
        private bool _dirty;
#endif

        /// <summary>Share of plants grown, 0 to 1. Written by <see cref="PresentationQuality.Apply"/>.</summary>
        public static float Density
        {
            get => _density;
            internal set => _density = value is > 0f and <= 1f
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Grass density must be above 0 and at most 1.");
        }

        /// <summary>Metres from the camera plants are drawn to. Written by <see cref="PresentationQuality.Apply"/>.</summary>
        public static float Distance
        {
            get => _distance;
            internal set => _distance = value > 0f
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Grass distance must be above 0.");
        }

        /// <summary>Metres from the camera plants cast shadows to, 0 for none. Written by <see cref="PresentationQuality.Apply"/>.</summary>
        public static float ShadowDistance
        {
            get => _shadowDistance;
            internal set => _shadowDistance = value >= 0f
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Grass shadow distance must not be negative.");
        }

        private static GrassSettings Settings => new(Density, Distance, ShadowDistance);

        /// <summary>Plants and draws the camera's last scatter holds, read back from the GPU for profiling.</summary>
        public (int Instances, int Draws) Drawn(Camera camera) =>
            _cameras.TryGet(camera, out var state) ? state.Batch.Drawn() : (0, 0);

        private void OnEnable()
        {
            Build();
            RenderPipelineManager.beginCameraRendering += Render;
        }

        protected override void OnDisabled()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            Release();
        }

#if UNITY_EDITOR
        // Rebuilt on the next camera, not here: OnValidate runs mid-import, too early to make GPU buffers.
        private void OnValidate() => _dirty = true;
#endif

        private void Build()
        {
#if UNITY_EDITOR
            _dirty = false;
#endif
            var problems = Problems();
            if (problems.Count > 0)
            {
                Debug.LogError($"HyperRTS: Grass Renderer can't draw: {string.Join("; ", problems)}.", this);
                return;
            }

            _kernels = new GrassComputeInputs(compute, field, terrain);
            _materials = field.plants.ConvertAll(Instanced).ToArray();
            _tiles = new ScatterTiles(field.tiles.Count, t => field.TileRect(field.tiles[t]), field.TallestPlant, terrain);
            _newState ??= NewState;
            _ready = true;
        }

        private GrassCameraState NewState() => new(field, _kernels, _materials);

        private Material Instanced(GrassPlant plant)
        {
            var material = new Material(shader) { name = $"{plant.name} (Grass)", hideFlags = HideFlags.DontSave };
            material.CopyPropertiesFromMaterial(plant.material);
            return material;
        }

        private void Release()
        {
            _ready = false;
            _cameras.Dispose();
            _kernels?.Dispose();
            _kernels = null;
            if (_materials != null)
            {
                Array.ForEach(_materials, Object.DestroyImmediate);
                _materials = null;
            }
        }

        private void Render(ScriptableRenderContext context, Camera camera)
        {
#if UNITY_EDITOR
            if (_dirty || (_ready && Stale()))
            {
                Release();
                Build();
            }
#endif

            if (!_ready || !CameraStates<GrassCameraState>.Draws(camera, gameObject))
            {
                return;
            }

            var state = _cameras.Get(camera, _newState);
            var position = camera.transform.position;
            var settings = Settings;
            _tiles.FindVisible(camera, position, settings.Distance);
            if (!state.Holds(position, settings, _tiles.Visible))
            {
                Queue(state.Batch, position, settings);
                _kernels.SetCamera(position, settings);
                state.Batch.Generate();
                state.Remember(position, settings, _tiles.Visible);
            }

            state.Batch.Draw(gameObject.layer, camera);
        }

#if UNITY_EDITOR
        // Catches a map rebuild that reshaped the field or its plants while the scene stayed open.
        private bool Stale()
        {
            var lost = field == null || terrain == null || compute == null || shader == null;
            if (lost || _kernels.Stale || _tiles.Count != field.tiles.Count)
            {
                return true;
            }

            foreach (var state in _cameras.All)
            {
                if (state.Batch.Stale())
                {
                    return true;
                }
            }

            return false;
        }
#endif

        /// <summary>Hands every visible tile's plants to the batch.</summary>
        private void Queue(GrassBatch batch, Vector3 position, GrassSettings settings)
        {
            batch.Begin();
            foreach (var t in _tiles.Visible)
            {
                var bounds = _tiles[t];
                var range = ScatterTiles.Distances(bounds, position);
                var tile = field.tiles[t];
                var rect = field.TileRect(tile);
                for (var i = tile.firstPlant; i < tile.firstPlant + tile.plantCount; i++)
                {
                    var grows = field.tilePlants[i];
                    var job = new GrassJob(rect, grows.plant, field.plants[grows.plant].Spacing);
                    batch.Add(job, grows.coverage, bounds, range, settings);
                }
            }
        }

        private List<string> Problems()
        {
            var problems = new List<string>();
            if (field == null)
            {
                problems.Add($"{nameof(field)} is missing");
            }
            else
            {
                problems.AddRange(field.Problems());
            }

            if (compute == null)
            {
                problems.Add($"{nameof(compute)} is missing");
            }

            if (shader == null)
            {
                problems.Add($"{nameof(shader)} is missing");
            }

            if (terrain == null)
            {
                problems.Add($"{nameof(terrain)} is missing");
            }

            return problems;
        }
    }
}
