using System;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// Draws ground markers, health bars, capture dials, garrison pips, the placement ghost and rally points with
    /// instanced meshes.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Rig + "Overlay Renderer")]
    [Icon(HyperRTSIcons.UI)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class OverlayRenderer : PresentationBehaviour
    {
        // Enough cells for a marker to follow the ground's bumps without costing much at 800 units.
        private const int MarkerGridCells = 12;

        [SerializeField]
        [Tooltip("HyperRTS/Overlay material; tinted per overlay through _BaseColor.")]
        private Material material;

        [SerializeField]
        [Tooltip("HyperRTS/Ground Marker material for selection and hover markers.")]
        private Material markerMaterial;

        [SerializeField]
        [Tooltip("HyperRTS/Health Bar material.")]
        private Material barMaterial;

        [SerializeField]
        [Tooltip("Overlay colours and sizes; a game supplies its own asset.")]
        private OverlayStyle style;

        private OverlayMaterials _materials;
        private Mesh[] _meshes;
        private UnitOverlays _units;
        private BuildingOverlays _buildings;
        private CommandOverlays _commands;

        /// <summary>Which units show a health bar; a game sets it from its settings.</summary>
        public HealthBarVisibility HealthBars { get; set; } = HealthBarVisibility.DamagedAndSelected;

        /// <summary>Multiplies the pixel size of health bars, from the game's UI scale.</summary>
        public float UIScale { get; set; } = 1f;

        protected override void OnAwake()
        {
            if (material == null || markerMaterial == null || barMaterial == null || style == null)
            {
                throw new InvalidOperationException(
                    $"Overlay Renderer on '{name}' needs its material, markerMaterial, barMaterial and style set.");
            }

            var ring = OverlayMeshes.Ring(48, 0.82f);
            var billboard = OverlayMeshes.Billboard();
            var box = OverlayMeshes.Box();
            var flat = OverlayMeshes.FlatQuad();
            var dial = BuildingOverlays.DialSegment();
            var disc = OverlayMeshes.Disc(48);
            var grid = OverlayMeshes.Grid(MarkerGridCells);
            _meshes = new[] { ring, billboard, box, flat, dial, disc, grid };
            _units = new UnitOverlays(grid, billboard, markerMaterial, barMaterial);
            _buildings = new BuildingOverlays(dial, disc, billboard);
            _commands = new CommandOverlays(box, ring, flat);
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || !MatchView.TryGetDefault(out var view))
            {
                return;
            }

            _materials ??= new OverlayMaterials(material);

            _units.Gather(view, style, HealthBars, Time.unscaledTime);
            _buildings.Gather(view, style, camera);
            _commands.Gather(view, style);
            _units.Draw(style, UIScale, gameObject.layer);
            _buildings.Draw(_materials, view, style, gameObject.layer);
            _commands.Draw(_materials.DepthTested, style, gameObject.layer);
        }

        protected override void OnDestroyed()
        {
            foreach (var mesh in _meshes)
            {
                Destroy(mesh);
            }

            _units.Dispose();
            _materials?.Dispose();
        }
    }
}
