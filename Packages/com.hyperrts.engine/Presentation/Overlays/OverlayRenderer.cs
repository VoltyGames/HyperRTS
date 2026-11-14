using HyperRTS.Presentation.Overlays.Bars;
using HyperRTS.Presentation.Overlays.Ghosts;
using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// Draws ground markers, health bars, capture dials, garrison pips, the placement outline and rally points, the
    /// cover preview and fire mission ranges with instanced meshes, and keeps the placement ghost's model copies.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Scene + "Rig/Overlay Renderer")]
    [Icon(HyperRTSIcons.OverlayRenderer)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class OverlayRenderer : PresentationBehaviour
    {
        // Enough cells for a marker to follow the ground's bumps without costing much at 800 units.
        private const int MarkerGridCells = 12;

        // Range rings span hundreds of metres, so their grid is finer to stay on the ground.
        private const int RangeGridCells = 48;

        [SerializeField]
        [Tooltip("HyperRTS/Overlay material; each instance carries its own colour.")]
        private Material material;

        [SerializeField]
        [Tooltip("HyperRTS/Ground Marker material for selection and hover markers.")]
        private Material markerMaterial;

        [SerializeField]
        [Tooltip("HyperRTS/Health Bar material.")]
        private Material barMaterial;

        [SerializeField]
        [Tooltip("HyperRTS/Placement Ghost material drawn over the placed building's real meshes.")]
        private Material ghostMaterial;

        [SerializeField]
        [Tooltip("Overlay colours and sizes; a game supplies its own asset.")]
        private OverlayStyle style;

        private OverlayMaterials _materials;
        private Mesh[] _meshes;
        private UnitOverlays _units;
        private BuildingOverlays _buildings;
        private CommandOverlays _commands;
        private FireMissionOverlay _fireMissions;
        private CoverPreview _cover;
        private PlacementGhosts _ghosts;

        /// <summary>Which units show a health bar; a game sets it from its settings.</summary>
        public HealthBarVisibility HealthBars { get; set; } = HealthBarVisibility.DamagedAndSelected;

        /// <summary>Multiplies the pixel size of health bars, from the game's UI scale.</summary>
        public float UIScale { get; set; } = 1f;

        protected override void OnAwake()
        {
            var missing = material == null || markerMaterial == null || barMaterial == null || ghostMaterial == null;
            if (missing || style == null)
            {
                throw new InvalidOperationException(
                    $"Overlay Renderer on '{name}' needs its material, markerMaterial, barMaterial, ghostMaterial and style set.");
            }

            var ring = OverlayMeshes.Ring(48, 0.82f);
            var billboard = OverlayMeshes.Billboard();
            var flat = OverlayMeshes.FlatQuad();
            var dial = BuildingOverlays.DialSegment();
            var disc = OverlayMeshes.Disc(48);
            var grid = OverlayMeshes.Grid(MarkerGridCells);
            var rangeGrid = OverlayMeshes.Grid(RangeGridCells);
            _meshes = new[] { ring, billboard, flat, dial, disc, grid, rangeGrid };
            _units = new UnitOverlays(grid, billboard, markerMaterial, barMaterial);
            _buildings = new BuildingOverlays(dial, disc, billboard);
            _commands = new CommandOverlays(ring, flat);
            _fireMissions = new FireMissionOverlay(rangeGrid, markerMaterial);
            _cover = new CoverPreview(ring);
            _ghosts = new PlacementGhosts(ghostMaterial);
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
            _fireMissions.Gather(view, style);
            _cover.Gather(view, style);
            _ghosts.Update(view, style);
            _units.Draw(style, UIScale, gameObject.layer);
            _buildings.Draw(_materials, gameObject.layer);
            _commands.Draw(_materials, gameObject.layer);
            _cover.Draw(_materials, gameObject.layer);
            _fireMissions.Draw(gameObject.layer);
        }

        protected override void OnDestroyed()
        {
            foreach (var mesh in _meshes)
            {
                Destroy(mesh);
            }

            _units.Dispose();
            _buildings.Dispose();
            _commands.Dispose();
            _fireMissions.Dispose();
            _cover.Dispose();
            _ghosts.Dispose();
            _materials?.Dispose();
        }
    }
}
