using System;
using System.Collections.Generic;
using HyperRTS.Core;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// Grows a map's row crops on the GPU every frame: the CPU culls <see cref="RowCropFields"/> tiles against the
    /// main camera's frustum and draw distance, RowCrops.compute grows each visible tile's plants from the fields'
    /// row lattices (dropping or flattening the ones vehicles crushed, read from the trample map) and the plants are
    /// drawn per distance band with <c>Graphics.RenderMeshIndirect</c>. Regrowing every visible tile every frame costs a
    /// few hundred thousand cheap threads and needs no cache to invalidate when the camera moves or crops get
    /// trampled. Map tools add one per map scene.
    /// </summary>
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class RowCropRenderer : MonoBehaviour
    {
        public const string KernelName = "Plant";

        private static float _density = 1f;
        private static float _distance = 300f;

        [Tooltip("The baked fields this map grows.")]
        public RowCropFields fields;

        [Tooltip("The RowCrops compute shader.")]
        public ComputeShader compute;

        [Tooltip("The terrain the crops stand on.")]
        public Terrain terrain;

        private readonly List<RowCropBatch> _batches = new();
        private readonly Plane[] _planes = new Plane[6];
        private RowCropKernelInputs _inputs;
        private Bounds[] _tileBounds;
        private Bounds _worldBounds;

        /// <summary>Share of the plants grown, 0 to 1; the game sets it from its vegetation quality.</summary>
        public static float Density
        {
            get => _density;
            set => _density = value is >= 0f and <= 1f
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Row crop density must be 0 to 1.");
        }

        /// <summary>Metres from the camera crops are drawn to; the game sets it from its vegetation quality.</summary>
        public static float Distance
        {
            get => _distance;
            set => _distance = value > 0f
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "Row crop distance must be above 0.");
        }

        private void OnEnable()
        {
            var problems = Problems();
            if (problems.Count > 0)
            {
                Debug.LogError($"HyperRTS: Row Crop Renderer can't draw: {string.Join("; ", problems)}.", this);
                enabled = false;
                return;
            }

            var kernel = compute.FindKernel(KernelName);
            _inputs = new RowCropKernelInputs(compute, kernel, fields, terrain);
            fields.crops.ForEach(crop => _batches.Add(new RowCropBatch(crop, compute, kernel)));
            MeasureTiles();
        }

        private void OnDisable()
        {
            _batches.ForEach(batch => batch.Dispose());
            _batches.Clear();
            _inputs?.Dispose();
            _inputs = null;
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            Queue(camera);
            _inputs.Set(camera.transform.position, Density, Distance);
            foreach (var batch in _batches)
            {
                batch.Generate();
                batch.Draw(_worldBounds, gameObject.layer);
            }
        }

        /// <summary>Hands every tile in view to the batches of the crops growing on it.</summary>
        private void Queue(Camera camera)
        {
            _batches.ForEach(batch => batch.Begin());
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            var position = camera.transform.position;
            for (var t = 0; t < fields.tiles.Count; t++)
            {
                var bounds = _tileBounds[t];
                var (near, far) = RowCropView.Distances(bounds, position);
                var inView = near < Distance && GeometryUtility.TestPlanesAABB(_planes, bounds);
                if (!inView)
                {
                    continue;
                }

                var tile = fields.tiles[t];
                var min = fields.TileRect(tile).min;
                for (var f = tile.firstField; f < tile.firstField + tile.fieldCount; f++)
                {
                    var field = fields.tileFields[f];
                    _batches[fields.fields[field].crop].Add(new RowCropJob(min, field), Distance, near, far);
                }
            }
        }

        /// <summary>Each tile's box from the ground under it, and the box round them all that the draws are culled by.</summary>
        private void MeasureTiles()
        {
            var height = 0f;
            fields.crops.ForEach(crop => height = Mathf.Max(height, crop.height));
            _tileBounds = new Bounds[fields.tiles.Count];
            for (var t = 0; t < _tileBounds.Length; t++)
            {
                var rect = fields.TileRect(fields.tiles[t]);
                var (low, high) = GroundRange(rect);
                _tileBounds[t] = RowCropView.TileBounds(rect, low, high, height);
                if (t == 0)
                {
                    _worldBounds = _tileBounds[t];
                }

                _worldBounds.Encapsulate(_tileBounds[t]);
            }
        }

        /// <summary>Lowest and highest ground over a tile, from a 3 by 3 grid of samples.</summary>
        private (float Low, float High) GroundRange(Rect rect)
        {
            var (low, high) = (float.MaxValue, float.MinValue);
            var baseHeight = terrain.GetPosition().y;
            for (var i = 0; i <= 2; i++)
            {
                for (var j = 0; j <= 2; j++)
                {
                    var point = new Vector3(rect.xMin + rect.width * i * 0.5f, 0f, rect.yMin + rect.height * j * 0.5f);
                    var ground = baseHeight + terrain.SampleHeight(point);
                    low = Mathf.Min(low, ground);
                    high = Mathf.Max(high, ground);
                }
            }

            return (low - 0.5f, high + 0.5f);
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
