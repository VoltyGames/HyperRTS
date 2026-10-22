using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Meshes;
using HyperRTS.Editor.Maps.Texturing;
using HyperRTS.Presentation.Environment;
using HyperRTS.Presentation.Environment.Grass;
using HyperRTS.Presentation.Environment.RowCrops;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>Writes the map's world around the gameplay: terrain, ground colour, row crops, grass, rivers, sky, ford decks and minimap.</summary>
    public static class MapEnvironmentWriter
    {
        private const string RowCropsCompute =
            "Packages/com.hyperrts.engine/Presentation/Environment/RowCrops/RowCrops.compute";

        private const string GrassCompute = "Packages/com.hyperrts.engine/Presentation/Environment/Grass/Grass.compute";

        private const string GrassShader =
            "Packages/com.hyperrts.engine/Presentation/Environment/Grass/FoliageGrass.shader";

        /// <summary>Written before the map's scene opens, since opening it unloads the climate the textures come from.</summary>
        public static (Material Material, Texture2D Picture) WriteGround(MapGeneration generation,
            TerrainData data)
        {
            var (climate, paths) = (generation.Project.Climate, generation.Project.Paths);
            var groundTint = MapGroundTintPainter.Write(paths.GroundTint, generation.Layout, generation.Land, generation.Edges,
                generation.Shapes, generation.Heights, climate.groundTint);
            var rows = MapRowPainter.Write(paths.RowMap, generation.Land, generation.Layout.Symmetry);
            var material = MapTerrainMaterial.Write(paths.TerrainMaterial, climate, data, groundTint, rows);
            var picture = MapGroundColor.Write(paths.Ground, generation.Splat, climate, groundTint);
            MapCropWriter.Write(paths.RowCrops, paths.RowCropIds, generation.Crops);
            MapGrassWriter.Write(paths.Grass, paths.GrassCoverage, generation.Grass, climate.plants);
            MapGroundMaterialPainter.Write(paths.GroundMaterials, generation.Land, climate);
            return (material, picture);
        }

        public static void Write(MapScene scene, MapGeneration generation, TerrainData data,
            (Material Material, Texture2D Picture) ground)
        {
            var (layout, climate) = (generation.Layout, generation.Project.Climate);
            var terrain = CreateTerrain(scene, data, ground.Material, climate, layout.TerrainOrigin);
            CreateGroundColor(scene, ground.Picture, layout.TerrainBounds);
            CreateRowCrops(scene, generation.Project.Paths.RowCrops, terrain);
            CreateGrass(scene, generation.Project.Paths.Grass, terrain);
            var height = scene.CreateEntityObject("Terrain Height");
            height.transform.position = layout.TerrainOrigin;
            height.AddComponent<TerrainHeightAuthoring>().terrainData = data;
            CreateGroundMaterials(scene, generation);
            CreateWater(scene, generation);
            CreateWeather(scene, climate, generation.Project.Spec.weather);
            CreateDecks(scene, generation.Shapes, layout.GroundLevel);
        }

        // Loaded by path because opening the scene unloads the picture WriteGround saved.
        private static void CreateGroundMaterials(MapScene scene, MapGeneration generation)
        {
            var path = generation.Project.Paths.GroundMaterials;
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                ?? throw new System.InvalidOperationException($"HyperRTS: the ground material map '{path}' is missing.");
            var land = generation.Land.Bounds;
            var go = scene.CreateEntityObject("Ground Materials");
            go.transform.position = new Vector3(land.xMin, 0f, land.yMin);
            var materials = go.AddComponent<GroundMaterialsAuthoring>();
            materials.map = map;
            materials.spacing = MapLand.CellSize;
        }

        /// <summary>Points the scene's minimap at a picture of the map.</summary>
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

        /// <summary>Publishes the ground picture to the foliage shaders.</summary>
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

        // Loaded by path because opening the scene unloads the fields WriteGround saved.
        private static void CreateRowCrops(MapScene scene, string fieldsPath, Terrain terrain)
        {
            var fields = AssetDatabase.LoadAssetAtPath<RowCropFields>(fieldsPath);
            if (fields == null)
            {
                return;
            }

            var compute = Engine<ComputeShader>(RowCropsCompute, "row crop compute shader");
            var go = new GameObject("Row Crops");
            go.SetActive(false);
            go.transform.SetParent(scene.Root, false);
            var renderer = go.AddComponent<RowCropRenderer>();
            renderer.fields = fields;
            renderer.compute = compute;
            renderer.terrain = terrain;
            go.SetActive(true);
        }

        // Loaded by path because opening the scene unloads the field WriteGround saved.
        private static void CreateGrass(MapScene scene, string fieldPath, Terrain terrain)
        {
            var field = AssetDatabase.LoadAssetAtPath<GrassField>(fieldPath);
            if (field == null)
            {
                return;
            }

            var go = new GameObject("Grass");
            go.SetActive(false);
            go.transform.SetParent(scene.Root, false);
            var renderer = go.AddComponent<GrassRenderer>();
            renderer.field = field;
            renderer.compute = Engine<ComputeShader>(GrassCompute, "grass compute shader");
            renderer.shader = Engine<Shader>(GrassShader, "grass shader");
            renderer.terrain = terrain;
            go.SetActive(true);
        }

        private static T Engine<T>(string path, string what) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new System.InvalidOperationException($"HyperRTS: the {what} '{path}' is missing.");

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

        /// <summary>The climate's weathers on a <see cref="WeatherSet"/>, the spec's instanced under it to light the bake.</summary>
        private static void CreateWeather(MapScene scene, MapClimate climate, string key)
        {
            var go = scene.CreateChild("Weather").gameObject;
            var set = go.AddComponent<WeatherSet>();
            set.defaultKey = key;
            foreach (var weather in climate.weathers)
            {
                set.atmospheres.Add(weather.key, weather.atmosphere);
            }

            PrefabUtility.InstantiatePrefab(climate.Weather(key).atmosphere, go.transform);
        }

        /// <summary>A deck nav area over each ford at the height units wade. Bridges carry their own.</summary>
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
