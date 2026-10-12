using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Meshes;
using HyperRTS.Presentation.Environment;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// The map's world around the gameplay: terrain and its baked heights, river surfaces, the climate's atmosphere
    /// and sky, walkable decks over the crossings, and the minimap picture.
    /// </summary>
    public static class MapEnvironmentWriter
    {
        public static Terrain Write(MapPaths paths, MapLayout layout, MapShapes shapes, TerrainData data,
            MapGroundLook ground, MapClimate climate, Scene entities, Transform root)
        {
            var origin = new Vector3(layout.TerrainBounds.xMin, -layout.Datum, layout.TerrainBounds.yMin);
            var terrain = CreateTerrain(data, ground.Material, climate, origin, root);
            CreateGroundColor(ground.Picture, layout.TerrainBounds, root);
            var height = MapSceneWriter.CreateEntityObject(entities, "Terrain Height");
            height.transform.position = origin;
            height.AddComponent<TerrainHeightAuthoring>().terrainData = data;
            CreateWater(paths, layout, shapes, climate, root);
            PrefabUtility.InstantiatePrefab(climate.atmosphere, root);
            ApplySky(climate);
            CreateDecks(shapes, entities, layout.Datum);
            return terrain;
        }

        /// <summary>Points the scene's minimap at a picture of the map, adding the component the first time.</summary>
        public static void SetMinimap(Transform root, Texture2D image)
        {
            var minimap = root.GetComponentInChildren<MinimapImage>();
            if (minimap == null)
            {
                minimap = new GameObject("Minimap Image").AddComponent<MinimapImage>();
                minimap.transform.SetParent(root, false);
            }

            minimap.image = image;
            EditorUtility.SetDirty(minimap);
        }

        /// <summary>Publishes the ground picture to the foliage shaders while the scene is open.</summary>
        private static void CreateGroundColor(Texture2D picture, Rect area, Transform root)
        {
            var go = new GameObject("Ground Color");
            go.SetActive(false);
            go.transform.SetParent(root, false);
            var ground = go.AddComponent<GroundColor>();
            ground.picture = picture;
            ground.area = area;
            go.SetActive(true);
        }

        private static Terrain CreateTerrain(TerrainData data, Material material, MapClimate climate, Vector3 origin,
            Transform root)
        {
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(root, false);
            go.transform.position = origin;
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = material;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 1000f;
            terrain.detailObjectDistance = climate.detailDistance;
            terrain.detailObjectDensity = 1f;
            terrain.treeDistance = climate.treeDistance;
            terrain.treeBillboardDistance = climate.treeDistance;
            terrain.allowAutoConnect = false;
            return terrain;
        }

        private static void CreateWater(MapPaths paths, MapLayout layout, MapShapes shapes, MapClimate climate,
            Transform root)
        {
            if (shapes.Rivers.Count == 0)
            {
                return;
            }

            var mesh = MapWaterMesh.Build(shapes, layout.Terrain.waterLevel - layout.Datum, layout.TerrainBounds);
            AssetDatabase.DeleteAsset(paths.Water);
            AssetDatabase.CreateAsset(mesh, paths.Water);
            var water = new GameObject("Water");
            water.transform.SetParent(root, false);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = climate.water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void ApplySky(MapClimate climate)
        {
            RenderSettings.skybox = climate.skybox;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = climate.fogColor;
            RenderSettings.fogStartDistance = climate.fogStart;
            RenderSettings.fogEndDistance = climate.fogEnd;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>A deck nav area over each ford, turned to span it, at the height units wade; bridges carry their own.</summary>
        private static void CreateDecks(MapShapes shapes, Scene entities, float datum)
        {
            for (var i = 0; i < shapes.Crossings.Count; i++)
            {
                var crossing = shapes.Crossings[i];
                if (crossing.IsBridge)
                {
                    continue;
                }

                var deck = MapSceneWriter.CreateEntityObject(entities, $"{crossing.Kind} {i + 1}");
                deck.transform.SetPositionAndRotation(
                    new Vector3(crossing.Position.x, crossing.Height - datum, crossing.Position.y),
                    Quaternion.Euler(0f, crossing.Yaw, 0f));
                var area = deck.AddComponent<NavAreaAuthoring>();
                area.kind = NavAreaKind.Deck;
                area.size = new Vector2(crossing.Width, crossing.Span + 2f);
            }
        }
    }
}
