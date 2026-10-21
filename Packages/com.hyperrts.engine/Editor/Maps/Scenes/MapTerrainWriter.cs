using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>Writes the generated ground into the map's TerrainData: heights, layers and trees.</summary>
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
            // Grass is the GrassRenderer's: an asset rebuilt in place keeps no terrain details.
            data.detailPrototypes = System.Array.Empty<DetailPrototype>();
            WriteTrees(data, trees, generation.Scenery, climate, layout);
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

        /// <summary>
        /// One terrain prototype per species and variant, each species' variants in a run, then one per scenery model.
        /// </summary>
        private static void WriteTrees(TerrainData data, List<MapTree> trees, List<MapScenery> scenery,
            MapClimate climate, MapLayout layout)
        {
            var first = new int[climate.trees.Count];
            var prototypes = new List<TreePrototype>();
            for (var i = 0; i < climate.trees.Count; i++)
            {
                first[i] = prototypes.Count;
                prototypes.AddRange(climate.trees[i].prefabs.Select(prefab => new TreePrototype { prefab = prefab }));
            }

            var models = new Dictionary<GameObject, int>();
            foreach (var model in scenery.Select(s => s.Model).Distinct())
            {
                models[model] = prototypes.Count;
                prototypes.Add(new TreePrototype { prefab = model });
            }

            data.treePrototypes = prototypes.ToArray();
            var instances = trees.Select(tree =>
                Instance(first[tree.Species] + tree.Variant, tree.Position, tree.Yaw, tree.Scale, layout));
            var props = scenery.Select(s => Instance(models[s.Model], s.Position, s.Yaw, s.Scale, layout));
            data.SetTreeInstances(instances.Concat(props).ToArray(), true);
        }

        private static TreeInstance Instance(int prototype, Vector2 position, float yaw, float scale, MapLayout layout)
        {
            var local = (position - layout.TerrainBounds.min) / layout.TerrainBounds.size;
            return new TreeInstance
            {
                prototypeIndex = prototype,
                position = new Vector3(local.x, 0f, local.y),
                widthScale = scale,
                heightScale = scale,
                rotation = yaw * Mathf.Deg2Rad,
                color = Color.white,
                lightmapColor = Color.white,
            };
        }
    }
}
