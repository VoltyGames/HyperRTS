using System.Linq;
using HyperRTS.Editor.Maps.Build;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// Low-detail farmland round the backdrop, out to the spec's <c>horizonWidth</c>, so a camera tilted at its closest
    /// zoom sees fields and tree lines fading into haze instead of the world's edge: eight terrain tiles sharing the
    /// map's layers and material, painted as large fields of the farmland kit's crops with straight margins, belts of
    /// the kit's trees along some field edges, and no colliders, navigation or props.
    /// </summary>
    public static class MapHorizonWriter
    {
        private const int Heights = 257;
        private const int Paint = 256;

        /// <summary>Metres a tile reaches under the map's edge, so no crack shows at the seam.</summary>
        private const float Tuck = 4f;

        public static void Write(MapScene scene, MapGeneration generation, Terrain main, Material material)
        {
            var width = generation.Layout.Terrain.horizonWidth;
            if (width <= 0f)
            {
                return;
            }

            var root = scene.CreateChild("Horizon");
            var bounds = generation.Layout.TerrainBounds;
            var land = new MapHorizonLand(generation);
            var tiles = new Terrain[3, 3];
            tiles[1, 1] = main;
            for (var ix = 0; ix < 3; ix++)
            {
                for (var iz = 0; iz < 3; iz++)
                {
                    if (ix != 1 || iz != 1)
                    {
                        var rect = TileRect(bounds, width, ix, iz);
                        tiles[ix, iz] = CreateTile(root, generation, land, material, rect, $"{ix}{iz}");
                    }
                }
            }

            for (var ix = 0; ix < 3; ix++)
            {
                for (var iz = 0; iz < 3; iz++)
                {
                    tiles[ix, iz].SetNeighbors(At(tiles, ix - 1, iz), At(tiles, ix, iz + 1), At(tiles, ix + 1, iz),
                        At(tiles, ix, iz - 1));
                }
            }
        }

        private static Terrain At(Terrain[,] tiles, int ix, int iz) =>
            ix is >= 0 and < 3 && iz is >= 0 and < 3 ? tiles[ix, iz] : null;

        // Column ix and row iz of a 3 by 3 grid whose middle cell is the map's terrain, each side tile reaching under it.
        private static Rect TileRect(Rect bounds, float width, int ix, int iz)
        {
            var (x0, x1) = Span(ix, bounds.xMin, bounds.xMax, width);
            var (z0, z1) = Span(iz, bounds.yMin, bounds.yMax, width);
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }

        // The span of grid column (or row) i: before the map, across it, or after it.
        private static (float From, float To) Span(int i, float min, float max, float width)
        {
            if (i == 0)
            {
                return (min - width, min + Tuck);
            }

            return i == 1 ? (min, max) : (max - Tuck, max + width);
        }

        private static Terrain CreateTile(Transform root, MapGeneration generation, MapHorizonLand land, Material material,
            Rect rect, string name)
        {
            var paths = generation.Project.Paths;
            var path = $"{paths.Generated}/{paths.Name}_Horizon_{name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, path);
            }

            var terrain = generation.Layout.Terrain;
            data.heightmapResolution = Heights;
            data.alphamapResolution = Paint;
            data.size = new Vector3(rect.width, terrain.maxHeight, rect.height);
            data.SetHeights(0, 0, land.Heights(rect, Heights, terrain.maxHeight, generation.Layout.TerrainBounds));
            data.terrainLayers = generation.Project.Climate.layers.Select(l => l.terrainLayer).ToArray();
            data.SetAlphamaps(0, 0, land.Weights(rect, Paint));
            data.treePrototypes = land.TreePrototypes;
            data.SetTreeInstances(land.Trees(rect).ToArray(), true);
            EditorUtility.SetDirty(data);

            var go = Terrain.CreateTerrainGameObject(data);
            Object.DestroyImmediate(go.GetComponent<TerrainCollider>());
            go.name = $"Horizon {name}";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(rect.xMin, generation.Layout.TerrainOrigin.y, rect.yMin);
            var tile = go.GetComponent<Terrain>();
            tile.materialTemplate = material;
            tile.drawInstanced = true;
            tile.heightmapPixelError = 8f;
            tile.basemapDistance = 1000f;
            tile.treeDistance = generation.Layout.Terrain.horizonWidth + 400f;
            tile.treeBillboardDistance = tile.treeDistance;
            tile.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tile.allowAutoConnect = false;
            return tile;
        }
    }
}
