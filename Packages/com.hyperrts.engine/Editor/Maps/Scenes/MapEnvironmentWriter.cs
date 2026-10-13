using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Meshes;
using HyperRTS.Editor.Maps.Texturing;
using HyperRTS.Presentation.Environment;
using HyperRTS.Presentation.Environment.RowCrops;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// The map's world around the gameplay: the terrain with its material and baked heights, the ground colour
    /// foliage blends into, the row crop renderer, river surfaces, the climate's atmosphere and sky, walkable decks
    /// over the fords, and the minimap picture.
    /// </summary>
    public static class MapEnvironmentWriter
    {
        private const string RowCropsCompute =
            "Packages/com.hyperrts.engine/Presentation/Environment/RowCrops/RowCrops.compute";

        /// <summary>
        /// The terrain material with its macro map, and the ground colour picture foliage blends into. Written before
        /// the map's scene opens: opening it unloads the climate the textures are made from.
        /// </summary>
        public static (Material Material, Texture2D Picture) WriteGround(MapGeneration generation,
            TerrainData data)
        {
            var (climate, paths) = (generation.Project.Climate, generation.Project.Paths);
            var macro = MapMacroPainter.Write(paths.Macro, generation.Layout, generation.Land, generation.Shapes);
            var material = MapTerrainMaterial.Write(paths.TerrainMaterial, climate, data, macro, generation.RowAngle);
            var picture = MapGroundColor.Write(paths.Ground, generation.Splat, climate, macro);
            MapCropWriter.Write(paths.RowCrops, paths.RowCropIds, generation.Crops);
            return (material, picture);
        }

        public static void Write(MapScene scene, MapGeneration generation, TerrainData data,
            (Material Material, Texture2D Picture) ground)
        {
            var (layout, climate) = (generation.Layout, generation.Project.Climate);
            var terrain = CreateTerrain(scene, data, ground.Material, climate, layout.TerrainOrigin);
            CreateGroundColor(scene, ground.Picture, layout.TerrainBounds);
            CreateRowCrops(scene, generation.Project.Paths.RowCrops, terrain);
            var height = scene.CreateEntityObject("Terrain Height");
            height.transform.position = layout.TerrainOrigin;
            height.AddComponent<TerrainHeightAuthoring>().terrainData = data;
            CreateWater(scene, generation);
            PrefabUtility.InstantiatePrefab(climate.atmosphere, scene.Root);
            ApplySky(climate);
            CreateDecks(scene, generation.Shapes, layout.GroundLevel);
        }

        /// <summary>Points the scene's minimap at a picture of the map, adding the component the first time.</summary>
        public static void SetMinimap(MapScene scene, Texture2D image)
        {
            var minimap = scene.Root.GetComponentInChildren<MinimapImage>();
            if (minimap == null)
            {
                minimap = scene.CreateChild("Minimap Image").gameObject.AddComponent<MinimapImage>();
            }

            minimap.image = image;
            EditorUtility.SetDirty(minimap);
        }

        /// <summary>Publishes the ground picture to the foliage shaders while the scene is open.</summary>
        private static void CreateGroundColor(MapScene scene, Texture2D picture, Rect area)
        {
            var go = new GameObject("Ground Color");
            go.SetActive(false);
            go.transform.SetParent(scene.Root, false);
            var ground = go.AddComponent<GroundColor>();
            ground.picture = picture;
            ground.area = area;
            go.SetActive(true);
        }

        /// <summary>The renderer that grows the map's row crops on the GPU; none when the map sows none.</summary>
        // Loaded by path: opening the map's scene unloads the fields WriteGround saved. None when the map sows no row crops.
        private static void CreateRowCrops(MapScene scene, string fieldsPath, Terrain terrain)
        {
            var fields = AssetDatabase.LoadAssetAtPath<RowCropFields>(fieldsPath);
            if (fields == null)
            {
                return;
            }

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(RowCropsCompute)
                ?? throw new System.InvalidOperationException($"HyperRTS: the row crop compute shader '{RowCropsCompute}' is missing.");
            var go = new GameObject("Row Crops");
            go.SetActive(false);
            go.transform.SetParent(scene.Root, false);
            var renderer = go.AddComponent<RowCropRenderer>();
            renderer.fields = fields;
            renderer.compute = compute;
            renderer.terrain = terrain;
            go.SetActive(true);
        }

        private static Terrain CreateTerrain(MapScene scene, TerrainData data, Material material, MapClimate climate,
            Vector3 origin)
        {
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(scene.Root, false);
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

        private static void CreateWater(MapScene scene, MapGeneration generation)
        {
            var (layout, shapes) = (generation.Layout, generation.Shapes);
            if (shapes.Rivers.Count == 0)
            {
                return;
            }

            var path = generation.Project.Paths.Water;
            var mesh = MapWaterMesh.Build(shapes, generation.Project.Climate, layout.WaterHeight, layout.TerrainBounds);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var water = scene.CreateChild("Water").gameObject;
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = generation.Project.Climate.water;
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
        private static void CreateDecks(MapScene scene, MapShapes shapes, float groundLevel)
        {
            for (var i = 0; i < shapes.Crossings.Count; i++)
            {
                var crossing = shapes.Crossings[i];
                if (crossing.IsBridge)
                {
                    continue;
                }

                var deck = scene.CreateEntityObject($"{crossing.Kind} {i + 1}");
                deck.transform.SetPositionAndRotation(
                    new Vector3(crossing.Position.x, crossing.Height - groundLevel, crossing.Position.y),
                    Quaternion.Euler(0f, crossing.Yaw, 0f));
                var area = deck.AddComponent<NavAreaAuthoring>();
                area.kind = NavAreaKind.Deck;
                area.size = new Vector2(crossing.Width, crossing.Span + 2f);
            }
        }
    }
}
