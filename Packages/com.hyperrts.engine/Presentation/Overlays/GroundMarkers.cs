using System;
using HyperRTS.Presentation.Environment;
using HyperRTS.Presentation.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>One frame's selection and hover markers, draped on the active terrain in one instanced draw.</summary>
    public sealed class GroundMarkers : IDisposable
    {
        private const float HoverLine = 0.6f;

        // Keeps the shader's corner radius non-zero so its sign can carry the bracket flag.
        private const float MinCorner = 0.01f;

        private static readonly int HeightId = Shader.PropertyToID("_Height");
        private static readonly int HeightAreaId = Shader.PropertyToID("_HeightArea");
        private static readonly int HeightScaleId = Shader.PropertyToID("_HeightScale");

        private readonly InstanceDataBatch _batch;
        private readonly Material _material;
        private Terrain _terrain;
        private TerrainHeightInput _heights;

        public GroundMarkers(Mesh grid, Material material)
        {
            _batch = new InstanceDataBatch(grid, "_Tint", "_Marker");
            _material = new Material(material) { hideFlags = HideFlags.DontSave };
            BindFlatGround();
        }

        public void Add(in MarkerPose pose, in MarkerFootprint footprint, Color color, bool selected, OverlayStyle style)
        {
            var line = selected ? style.markerLine : style.markerLine * HoverLine;
            var fill = selected ? style.markerFill : 0f;
            var corner = Mathf.Max(footprint.CornerRadius, MinCorner);
            var signedCorner = footprint.Shape == MarkerShape.Brackets ? -corner : corner;
            _batch.Add(pose.Matrix(footprint), color, new Vector4(line, style.markerFade, fill, signedCorner));
        }

        public void Draw(int layer)
        {
            BindTerrain(Terrain.activeTerrain);
            _batch.Draw(_material, layer);
        }

        public void Dispose() => Object.Destroy(_material);

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

        // Without a terrain the ground is flat at y = 0, as TerrainHeight has it.
        private void BindFlatGround()
        {
            _terrain = null;
            _material.SetTexture(HeightId, Texture2D.blackTexture);
            _material.SetVector(HeightAreaId, new Vector4(0f, 0f, 1f, 1f));
            _material.SetVector(HeightScaleId, new Vector4(0f, 0f, 1f, 0f));
        }
    }
}
