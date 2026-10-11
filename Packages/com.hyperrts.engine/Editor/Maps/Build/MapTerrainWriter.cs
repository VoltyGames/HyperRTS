using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Ground;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>Writes the generated ground into the map's TerrainData asset: heights, layers, grass and trees.</summary>
    public static class MapTerrainWriter
    {
        private const int DetailPatch = 32;

        /// <summary>About three splat texels per metre keep road edges crisp.</summary>
        public static int AlphamapResolution(MapLayout layout) =>
            Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(layout.TerrainBounds.width * 2.5f)), 512, 2048);

        public static int DetailResolution(MapLayout layout) =>
            Mathf.CeilToInt(layout.TerrainBounds.width * 1.25f / DetailPatch) * DetailPatch;

        public static TerrainData Write(string path, MapLayout layout, MapHeights heights, float[,,] splat,
            List<int[,]> details, List<MapTree> trees, MapClimate climate)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, path);
            }

            var terrain = layout.Terrain;
            data.heightmapResolution = terrain.resolution;
            data.size = new Vector3(layout.TerrainBounds.width, terrain.maxHeight, layout.TerrainBounds.height);
            data.SetHeights(0, 0, Normalised(heights, terrain.maxHeight));
            data.terrainLayers = climate.layers.Select(l => l.terrainLayer).ToArray();
            data.alphamapResolution = splat.GetLength(0);
            data.SetAlphamaps(0, 0, splat);
            WriteDetails(data, details, climate);
            data.treePrototypes = climate.trees.Select(t => new TreePrototype { prefab = t.prefab }).ToArray();
            data.SetTreeInstances(trees.Select(t => Instance(t, layout)).ToArray(), true);
            data.wavingGrassAmount = 0.25f;
            data.wavingGrassSpeed = 0.4f;
            data.wavingGrassStrength = 0.35f;
            data.wavingGrassTint = new Color(0.85f, 0.85f, 0.75f);
            EditorUtility.SetDirty(data);
            return data;
        }

        private static float[,] Normalised(MapHeights heights, float maxHeight)
        {
            var samples = new float[heights.Resolution, heights.Resolution];
            for (var z = 0; z < heights.Resolution; z++)
            {
                for (var x = 0; x < heights.Resolution; x++)
                {
                    samples[z, x] = heights[x, z] / maxHeight;
                }
            }

            return samples;
        }

        private static void WriteDetails(TerrainData data, List<int[,]> details, MapClimate climate)
        {
            var resolution = details.Count > 0 ? details[0].GetLength(0) : DetailPatch;
            data.SetDetailResolution(resolution, DetailPatch);
            data.SetDetailScatterMode(DetailScatterMode.CoverageMode);
            data.detailPrototypes = climate.details.Select(Prototype).ToArray();
            for (var i = 0; i < details.Count; i++)
            {
                data.SetDetailLayer(0, 0, i, details[i]);
            }
        }

        private static DetailPrototype Prototype(MapClimateDetail detail)
        {
            var mesh = detail.mesh != null;
            return new DetailPrototype
            {
                usePrototypeMesh = mesh,
                prototype = detail.mesh,
                prototypeTexture = mesh ? null : detail.texture,
                renderMode = mesh ? DetailRenderMode.VertexLit : DetailRenderMode.GrassBillboard,
                useInstancing = mesh,
                minWidth = detail.width.x,
                maxWidth = detail.width.y,
                minHeight = detail.height.x,
                maxHeight = detail.height.y,
                healthyColor = detail.healthy,
                dryColor = detail.dry,
                noiseSpread = 0.4f,
                density = detail.density,
                alignToGround = mesh ? 0.6f : 0f,
            };
        }

        private static TreeInstance Instance(MapTree tree, MapLayout layout)
        {
            var local = (tree.Position - layout.TerrainBounds.min) / layout.TerrainBounds.size;
            return new TreeInstance
            {
                prototypeIndex = tree.Prototype,
                position = new Vector3(local.x, 0f, local.y),
                widthScale = tree.Scale,
                heightScale = tree.Scale,
                rotation = tree.Yaw * Mathf.Deg2Rad,
                color = Color.white,
                lightmapColor = Color.white,
            };
        }
    }
}
