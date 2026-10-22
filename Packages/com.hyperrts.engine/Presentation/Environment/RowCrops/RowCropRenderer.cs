using HyperRTS.Presentation.Common;
using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Rendering;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// Grows a map's row crops on the GPU from <see cref="RowCropFields"/> for each camera about to render, in edit
    /// mode too, and draws them per distance band. Each camera keeps its own buffers and regrows only when its view,
    /// the quality settings or the trample map change.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed partial class RowCropRenderer : PresentationBehaviour
    {
        [AutoStaticsCleanup]
        private static float _density = PresentationQuality.Default.CropDensity;
        [AutoStaticsCleanup]
        private static float _distance = PresentationQuality.Default.CropDistance;

        [Tooltip("The baked fields this map grows.")]
        public RowCropFields fields;

        [Tooltip("The RowCrops compute shader.")]
        public ComputeShader compute;

        [Tooltip("The terrain the crops stand on.")]
        public Terrain terrain;

        private readonly CameraStates<RowCropCameraState> _cameras = new();
        private Func<RowCropCameraState> _newState;
        private RowCropCompute _kernels;
        private RowCropComputeInputs _inputs;
        private ScatterTiles _tiles;
        private bool _ready;
#if UNITY_EDITOR
        private bool _dirty;
#endif

        /// <summary>Share of plants grown, 0 to 1. Written by <see cref="PresentationQuality.Apply"/>.</summary>
        public static float Density
        {
            get => _density;
            internal set => _density = value is >= 0f and <= 1f
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Row crop density must be 0 to 1.");
        }

        /// <summary>Metres from the camera crops are drawn to; written by <see cref="PresentationQuality"/>.</summary>
        public static float Distance
        {
            get => _distance;
            internal set => _distance = value > 0f
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Row crop distance must be above 0.");
        }

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
                Debug.LogError($"HyperRTS: Row Crop Renderer can't draw: {string.Join("; ", problems)}.", this);
                return;
            }

            _kernels = new RowCropCompute(compute);
            _inputs = new RowCropComputeInputs(_kernels, fields, terrain);
            _tiles = new ScatterTiles(fields.tiles.Count, t => fields.TileRect(fields.tiles[t]), fields.TallestPlant, terrain);
            _newState ??= NewState;
            _ready = true;
        }

        private RowCropCameraState NewState() => new(fields, _kernels);

        private void Release()
        {
            _ready = false;
            _cameras.Dispose();
            _inputs?.Dispose();
            _inputs = null;
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

            if (!_ready || !CameraStates<RowCropCameraState>.Draws(camera, gameObject))
            {
                return;
            }

            var state = _cameras.Get(camera, _newState);
            var position = camera.transform.position;
            _tiles.FindVisible(camera, position, Distance);
            if (!state.Holds(position, Density, Distance, _tiles.Visible))
            {
                Grow(state, position);
            }

            foreach (var batch in state.Batches)
            {
                batch.Draw(gameObject.layer, camera);
            }
        }

        private void Grow(RowCropCameraState state, Vector3 position)
        {
            Queue(state.Batches, position);
            _inputs.SetCamera(position, Density, Distance);
            foreach (var batch in state.Batches)
            {
                batch.Generate();
            }

            state.Remember(position, Density, Distance, _tiles.Visible);
        }

#if UNITY_EDITOR
        // Catches a map rebuild that reshaped the fields or the crops while the scene stayed open.
        private bool Stale()
        {
            var lost = fields == null || terrain == null || compute == null;
            if (lost || _inputs.Stale || _tiles.Count != fields.tiles.Count)
            {
                return true;
            }

            foreach (var state in _cameras.All)
            {
                if (state.Stale(fields))
                {
                    return true;
                }
            }

            return false;
        }
#endif

        /// <summary>Hands every visible tile to the batches of the crops growing on it.</summary>
        private void Queue(List<RowCropBatch> batches, Vector3 position)
        {
            batches.ForEach(batch => batch.Begin());
            foreach (var t in _tiles.Visible)
            {
                var bounds = _tiles[t];
                var (near, far) = ScatterTiles.Distances(bounds, position);
                var tile = fields.tiles[t];
                var rect = fields.TileRect(tile);
                for (var f = tile.firstField; f < tile.firstField + tile.fieldCount; f++)
                {
                    var index = fields.tileFields[f];
                    var field = fields.fields[index];
                    var crop = fields.crops[field.crop];
                    var job = new RowCropJob(rect, index, field, crop.rowSpacing, crop.plantSpacing);
                    batches[field.crop].Add(job, bounds, Distance, near, far);
                }
            }
        }

        private List<string> Problems()
        {
            var problems = new List<string>();
            if (fields == null)
            {
                problems.Add($"{nameof(fields)} is missing");
            }
            else
            {
                problems.AddRange(fields.Problems());
            }

            if (compute == null)
            {
                problems.Add($"{nameof(compute)} is missing");
            }

            if (terrain == null)
            {
                problems.Add($"{nameof(terrain)} is missing");
            }

            return problems;
        }
    }
}
