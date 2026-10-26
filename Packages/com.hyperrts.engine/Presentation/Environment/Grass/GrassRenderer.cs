using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Rendering;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
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
    public sealed partial class GrassRenderer : ScatterRenderer<GrassCameraState>
    {
        [AutoStaticsCleanup]
        private static float _density = PresentationQuality.Default.GrassDensity;
        [AutoStaticsCleanup]
        private static float _distance = PresentationQuality.Default.GrassDistance;
        [AutoStaticsCleanup]
        private static float _shadowDistance = PresentationQuality.Default.GrassShadowDistance;

        [Tooltip("The baked plants this map grows.")]
        public GrassField field;

        [Tooltip("The Hidden/HyperRTS/FoliageGrass shader each plant's material is copied onto.")]
        public Shader shader;

        private GrassComputeInputs _kernels;
        private Material[] _materials;

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

        private protected override float DrawDistance => Distance;

        /// <summary>Plants and draws the camera's last scatter holds, read back from the GPU for profiling.</summary>
        public (int Instances, int Draws) Drawn(Camera camera) =>
            Cameras.TryGet(camera, out var state) ? state.Batch.Drawn() : (0, 0);

        private protected override void BuildInputs()
        {
            _kernels = new GrassComputeInputs(compute, field, terrain);
            _materials = field.plants.ConvertAll(Instanced).ToArray();
        }

        private protected override ScatterTiles NewTiles() =>
            new(field.tiles.Count, t => field.TileRect(field.tiles[t]), field.TallestPlant, terrain);

        private protected override GrassCameraState NewState() => new(field, _kernels, _materials);

        private Material Instanced(GrassPlant plant)
        {
            var material = new Material(shader) { name = $"{plant.name} (Grass)", hideFlags = HideFlags.DontSave };
            material.CopyPropertiesFromMaterial(plant.material);
            return material;
        }

        private protected override void ReleaseInputs()
        {
            _kernels?.Dispose();
            _kernels = null;
            if (_materials != null)
            {
                Array.ForEach(_materials, Object.DestroyImmediate);
                _materials = null;
            }
        }

        private protected override void Draw(GrassCameraState state, Camera camera, Vector3 position)
        {
            var settings = Settings;
            if (!state.Holds(position, settings, Tiles.Visible))
            {
                Queue(state.Batch, position, settings);
                _kernels.SetCamera(position, settings);
                state.Batch.Generate();
                state.Remember(position, settings, Tiles.Visible);
            }

            state.Batch.Draw(gameObject.layer, camera);
        }

#if UNITY_EDITOR
        // Catches a map rebuild that reshaped the field or its plants while the scene stayed open.
        private protected override bool InputsStale()
        {
            if (field == null || shader == null)
            {
                return true;
            }

            return _kernels.Stale || Tiles.Count != field.tiles.Count;
        }

        private protected override bool Stale(GrassCameraState state) => state.Batch.Stale();
#endif

        /// <summary>Hands every visible tile's plants to the batch.</summary>
        private void Queue(GrassBatch batch, Vector3 position, GrassSettings settings)
        {
            batch.Begin();
            foreach (var t in Tiles.Visible)
            {
                var bounds = Tiles[t];
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

        private protected override void AddProblems(List<string> problems)
        {
            if (field == null)
            {
                problems.Add($"{nameof(field)} is missing");
            }
            else
            {
                problems.AddRange(field.Problems());
            }

            if (shader == null)
            {
                problems.Add($"{nameof(shader)} is missing");
            }
        }
    }
}
