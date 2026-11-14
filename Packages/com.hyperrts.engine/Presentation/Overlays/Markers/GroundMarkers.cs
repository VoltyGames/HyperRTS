using System;
using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Presentation.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Overlays.Markers
{
    /// <summary>One frame's selection and hover markers, draped on the active terrain in one draw.</summary>
    public sealed class GroundMarkers : IDisposable
    {
        private const float HoverLine = 0.6f;

        // Keeps the shader's corner radius non-zero so its sign can carry the bracket flag.
        private const float MinCorner = 0.01f;

        private readonly InstanceBuffer<MarkerInstance> _markers;
        private readonly Material _material;
        private Terrain _terrain;
        private TerrainHeightInput _heights;

        public GroundMarkers(Mesh grid, Material material)
        {
            _markers = new InstanceBuffer<MarkerInstance>(grid);
            _material = new Material(material) { hideFlags = HideFlags.DontSave };
            BindFlatGround();
        }

        public void Add(in MarkerPose pose, in MarkerFootprint footprint, Color color, bool selected, OverlayStyle style)
        {
            var line = selected ? style.markerLine : style.markerLine * HoverLine;
            var fill = selected ? style.markerFill : 0f;
            Add(pose, footprint, color, new MarkerLook(line, style.markerFade, fill));
        }

        public void Add(in MarkerPose pose, in MarkerFootprint footprint, Color color, in MarkerLook look)
        {
            var corner = Mathf.Max(footprint.CornerRadius, MinCorner);
            var signedCorner = footprint.Shape == MarkerShape.Brackets ? -corner : corner;
            _markers.Add(new MarkerInstance
            {
                ObjectToWorld = pose.Matrix(footprint),
                Tint = color,
                Shape = new Vector4(look.Line, look.Fade, look.Fill, signedCorner),
            });
        }

        public void Draw(int layer)
        {
            BindTerrain(Terrain.activeTerrain);
            _markers.Draw(_material, layer);
        }

        public void Dispose()
        {
            _markers.Dispose();
            Object.Destroy(_material);
        }

        // A map rebuild or scene change swaps the terrain or its heightmap texture.
        private void BindTerrain(Terrain terrain)
        {
            if (terrain == null)
            {
                if (_terrain != null)
                {
                    BindFlatGround();
                }

                return;
            }

            if (terrain != _terrain || _heights.Stale)
            {
                _terrain = terrain;
                _heights = new TerrainHeightInput(_material, terrain);
            }
        }

        private void BindFlatGround()
        {
            _terrain = null;
            TerrainHeightInput.BindFlat(_material);
        }
    }
}
