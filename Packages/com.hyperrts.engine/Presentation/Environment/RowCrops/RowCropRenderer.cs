using System;
using System.Collections.Generic;
using HyperRTS.Core;
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
    public sealed class RowCropRenderer : MonoBehaviour
    {
        private static float _density = 1f;
        private static float _distance = 300f;

        [Tooltip("The baked fields this map grows.")]
        public RowCropFields fields;

        [Tooltip("The RowCrops compute shader.")]
        public ComputeShader compute;

        [Tooltip("The terrain the crops stand on.")]
        public Terrain terrain;

        private readonly Dictionary<Camera, RowCropCameraState> _cameras = new();
        private readonly List<int> _visible = new();
        private readonly Plane[] _planes = new Plane[6];
        private RowCropKernels _kernels;
        private RowCropKernelInputs _inputs;
        private Bounds[] _tileBounds;
        private bool _ready;
#if UNITY_EDITOR
        private bool _dirty;
#endif

        /// <summary>Share of plants grown, 0 to 1. Set by the game from its vegetation quality.</summary>
        public static float Density
        {
            get => _density;
            set => _density = value is >= 0f and <= 1f
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Row crop density must be 0 to 1.");
        }

        /// <summary>Metres from the camera crops are drawn to. Set by the game from its vegetation quality.</summary>
        public static float Distance
        {
            get => _distance;
            set => _distance = value > 0f
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Row crop distance must be above 0.");
        }

        private void OnEnable()
        {
            Build();
            RenderPipelineManager.beginCameraRendering += Render;
        }

        private void OnDisable()
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

            _kernels = new RowCropKernels(compute);
            _inputs = new RowCropKernelInputs(_kernels, fields, terrain);
            _tileBounds = RowCropTileBounds.Measure(fields, terrain);
            _ready = true;
        }

        private void Release()
        {
            _ready = false;
            foreach (var state in _cameras.Values)
            {
                state.Dispose();
            }

            _cameras.Clear();
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

            if (!_ready || !Draws(camera))
            {
                return;
            }

            var state = State(camera);
            var position = camera.transform.position;
            FindVisible(camera, position);
            if (!state.Holds(position, Density, Distance, _visible))
            {
                Grow(state, position);
            }

            foreach (var batch in state.Batches)
            {
                batch.Draw(gameObject.layer, camera);
            }
        }

        /// <summary>
        /// Game and Scene view cameras that see this layer and this scene. Reflection probes and asset preview cameras
        /// are skipped: they render other scenes or bake once, and would regrow every crop for nothing.
        /// </summary>
        private bool Draws(Camera camera)
        {
            var kind = camera.cameraType is CameraType.Game or CameraType.SceneView;
            var seesLayer = (camera.cullingMask & (1 << gameObject.layer)) != 0;
            var ownScene = !camera.scene.IsValid() || camera.scene == gameObject.scene;
            return kind && seesLayer && ownScene;
        }

        private void Grow(RowCropCameraState state, Vector3 position)
        {
            Queue(state.Batches, position);
            _inputs.SetCamera(position, Density, Distance);
            foreach (var batch in state.Batches)
            {
                batch.Generate();
            }

            state.Remember(position, Density, Distance, _visible);
        }

        private RowCropCameraState State(Camera camera)
        {
            if (_cameras.TryGetValue(camera, out var state))
            {
                return state;
            }

            ForgetClosedCameras();
            state = new RowCropCameraState(fields, _kernels);
            _cameras.Add(camera, state);
            return state;
        }

        // Frees the buffers of cameras destroyed since, such as a closed Scene view.
        private void ForgetClosedCameras()
        {
            var closed = new List<Camera>();
            foreach (var camera in _cameras.Keys)
            {
                if (camera == null)
                {
                    closed.Add(camera);
                }
            }

            foreach (var camera in closed)
            {
                _cameras[camera].Dispose();
                _cameras.Remove(camera);
            }
        }

#if UNITY_EDITOR
        // Catches a map rebuild that reshaped the fields or the crops while the scene stayed open.
        private bool Stale()
        {
            var lost = fields == null || terrain == null || compute == null;
            if (lost || _inputs.Stale || _tileBounds.Length != fields.tiles.Count)
            {
                return true;
            }

            foreach (var state in _cameras.Values)
            {
                if (state.Stale(fields))
                {
                    return true;
                }
            }

            return false;
        }
#endif

        /// <summary>Indices of the tiles in a camera's view and draw distance, in tile order.</summary>
        private void FindVisible(Camera camera, Vector3 position)
        {
            _visible.Clear();
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            for (var t = 0; t < _tileBounds.Length; t++)
            {
                var bounds = _tileBounds[t];
                var near = RowCropView.Distances(bounds, position).Near;
                var inView = near < Distance && GeometryUtility.TestPlanesAABB(_planes, bounds);
                if (inView)
                {
                    _visible.Add(t);
                }
            }
        }

        /// <summary>Hands every visible tile to the batches of the crops growing on it.</summary>
        private void Queue(List<RowCropBatch> batches, Vector3 position)
        {
            batches.ForEach(batch => batch.Begin());
            foreach (var t in _visible)
            {
                var bounds = _tileBounds[t];
                var (near, far) = RowCropView.Distances(bounds, position);
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
