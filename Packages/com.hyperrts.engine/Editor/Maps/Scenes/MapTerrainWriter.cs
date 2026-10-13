using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Layout;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>Writes the generated ground into the map's TerrainData asset: heights, layers, grass and trees.</summary>
    public static class MapTerrainWriter
    {
        public static TerrainData Write(MapGeneration generation)
        {
            var (path, layout, climate) = (generation.Project.Paths.Terrain, generation.Layout, generation.Project.Climate);
            var (heights, splat, trees) = (generation.Heights, generation.Splat, generation.TerrainTrees);
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
            WriteDetails(data, generation.Details, climate);
            WriteTrees(data, trees, climate, layout);
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
            var resolution = details.Count > 0 ? details[0].GetLength(0) : MapLayout.DetailPatch;
            data.SetDetailResolution(resolution, MapLayout.DetailPatch);
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

        /// <summary>One terrain prototype per (species, variant), each species' variants in a run from its first.</summary>
        private static void WriteTrees(TerrainData data, List<MapTree> trees, MapClimate climate, MapLayout layout)
        {
            var first = new int[climate.trees.Count];
            var prototypes = new List<TreePrototype>();
            for (var i = 0; i < climate.trees.Count; i++)
            {
                first[i] = prototypes.Count;
                prototypes.AddRange(climate.trees[i].prefabs.Select(prefab => new TreePrototype { prefab = prefab }));
            }

            data.treePrototypes = prototypes.ToArray();
            var instances = trees.Select(tree =>
                Instance(tree, first[tree.Species] + climate.trees[tree.Species].Variant(tree.Seed), layout));
            data.SetTreeInstances(instances.ToArray(), true);
        }

        private static TreeInstance Instance(MapTree tree, int prototype, MapLayout layout)
        {
            var local = (tree.Position - layout.TerrainBounds.min) / layout.TerrainBounds.size;
            return new TreeInstance
            {
                prototypeIndex = prototype,
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
