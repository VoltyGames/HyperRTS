using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using HyperRTS.Presentation.Quality;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>
    /// A map's row crops past their plants: per crop, a raised field surface in 64 m blocks, cut from the field id
    /// picture and the terrain when the renderer builds its inputs, kept in memory only and drawn per camera.
    /// </summary>
    internal sealed class RowCropCanopy : IDisposable
    {
        public const float BlockSize = 64f;

        // RowCrops.compute's FADE_SHARE: the canopy fades out where the plants would shrink away.
        private const float FarFadeShare = 0.15f;

        private static readonly int HeightId = Shader.PropertyToID("_CanopyHeight");
        private static readonly int TileId = Shader.PropertyToID("_CanopyTile");
        private static readonly int FadeId = Shader.PropertyToID("_CanopyFade");
        private static readonly int CameraId = Shader.PropertyToID("_CanopyCamera");
        private static readonly int PlantingId = Shader.PropertyToID("_CanopyPlanting");
        private static readonly int GrowthId = Shader.PropertyToID("_Growth");
        private static readonly int LodgingId = Shader.PropertyToID("_Lodging");

        private readonly RowCropFields _fields;
        private readonly RowCropComputeInputs _inputs;
        private readonly List<(Mesh Mesh, int Crop)> _blocks = new();
        private readonly MaterialPropertyBlock[] _properties;
        private readonly float[] _wallTiles;

        public RowCropCanopy(RowCropFields fields, Terrain terrain, RowCropComputeInputs inputs)
        {
            _fields = fields;
            _inputs = inputs;
            _properties = new MaterialPropertyBlock[fields.crops.Count];
            _wallTiles = new float[fields.crops.Count];
            var ids = fields.fieldIds;
            var pixels = ids.GetPixels32();
            var decoded = Array.ConvertAll(pixels, RowCropFields.Decode);
            var fieldCrops = fields.fields.ConvertAll(field => field.crop);
            var heights = GroundHeights(fields.area, ids.width, ids.height, terrain);
            var shape = new RowCropCanopyShape(fields.area, ids.width, ids.height, decoded, fieldCrops, heights, BlockSize);
            for (var c = 0; c < fields.crops.Count; c++)
            {
                var crop = fields.crops[c];
                _properties[c] = new MaterialPropertyBlock();
                _wallTiles[c] = WallTile(crop);
                if (!crop.HasCanopy)
                {
                    continue;
                }

                foreach (var geometry in shape.Build(c, _wallTiles[c]))
                {
                    _blocks.Add((geometry.CreateMesh($"{crop.name} Canopy", crop.CanopyRise), c));
                }
            }
        }

        /// <summary>Whether the crops changed what the meshes were cut for: their number, assets, canopies or wall tile length.</summary>
        public bool Stale()
        {
            if (_fields.crops.Count != _wallTiles.Length)
            {
                return true;
            }

            for (var c = 0; c < _wallTiles.Length; c++)
            {
                var crop = _fields.crops[c];
                if (crop == null || WallTile(crop) != _wallTiles[c])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Draws the blocks a camera can see the canopy in, between each crop's fade-in and the draw distance.</summary>
        public void Draw(Camera camera, int layer, ScatterSettings settings)
        {
            for (var c = 0; c < _properties.Length; c++)
            {
                SetProperties(c, settings, camera.transform.position);
            }

            var position = camera.transform.position;
            var shadows = PresentationQuality.Current.CanopyCastsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (var (mesh, c) in _blocks)
            {
                var crop = _fields.crops[c];
                var (near, far) = ScatterTiles.Distances(mesh.bounds, position);
                if (near >= settings.Distance || far < crop.CanopyStart)
                {
                    continue;
                }

                // Unity culls each block by its bounds, for the camera and for each shadow cascade.
                var parameters = new RenderParams(crop.canopyMaterial)
                {
                    camera = camera,
                    layer = layer,
                    worldBounds = mesh.bounds,
                    shadowCastingMode = shadows,
                    receiveShadows = true,
                    matProps = _properties[c],
                };
                Graphics.RenderMesh(parameters, mesh, 0, Matrix4x4.identity);
            }
        }

        // 0 for a crop without a canopy, so adding or removing one rebuilds the meshes.
        private static float WallTile(RowCrop crop) => crop.HasCanopy ? crop.CanopyTile.y : 0f;

        public void Dispose() => _blocks.ForEach(block => CoreUtils.Destroy(block.Mesh));

        private void SetProperties(int c, ScatterSettings settings, Vector3 camera)
        {
            var crop = _fields.crops[c];
            var block = _properties[c];
            var tile = crop.CanopyTile;
            block.SetFloat(HeightId, crop.height);
            block.SetVector(TileId, new Vector4(1f / tile.x, 1f / tile.y, 0f, 0f));
            block.SetVector(FadeId, new Vector4(crop.lod1Distance, crop.canopyFade, settings.Distance,
                1f / (FarFadeShare * settings.Distance)));
            block.SetVector(GrowthId, crop.Growth);
            block.SetVector(LodgingId, crop.Lodging);
            block.SetVector(CameraId, camera);
            block.SetVector(PlantingId, new Vector4(crop.rowSpacing, crop.plantSpacing, 0f, 0f));
            _inputs.Bind(block);
        }

        // The ground under each texel's centre, read from the heightmap once and blended between its samples.
        private static float[] GroundHeights(Rect area, int width, int height, Terrain terrain)
        {
            var data = terrain.terrainData;
            var resolution = data.heightmapResolution;
            var samples = data.GetHeights(0, 0, resolution, resolution);
            var (origin, size) = (terrain.GetPosition(), data.size);
            var cell = new Vector2(area.width / width, area.height / height);
            var heights = new float[width * height];
            for (var j = 0; j < height; j++)
            {
                var z = area.yMin + (j + 0.5f) * cell.y;
                var v = Mathf.Clamp01((z - origin.z) / size.z) * (resolution - 1);
                for (var i = 0; i < width; i++)
                {
                    var x = area.xMin + (i + 0.5f) * cell.x;
                    var u = Mathf.Clamp01((x - origin.x) / size.x) * (resolution - 1);
                    heights[j * width + i] = origin.y + Bilinear(samples, u, v, resolution) * size.y;
                }
            }

            return heights;
        }

        // Samples are indexed [z, x], as TerrainData.GetHeights returns them.
        private static float Bilinear(float[,] samples, float u, float v, int resolution)
        {
            var (x, z) = (Mathf.Min((int)u, resolution - 2), Mathf.Min((int)v, resolution - 2));
            var (fx, fz) = (u - x, v - z);
            var south = Mathf.Lerp(samples[z, x], samples[z, x + 1], fx);
            var north = Mathf.Lerp(samples[z + 1, x], samples[z + 1, x + 1], fx);
            return Mathf.Lerp(south, north, fz);
        }
    }
}
