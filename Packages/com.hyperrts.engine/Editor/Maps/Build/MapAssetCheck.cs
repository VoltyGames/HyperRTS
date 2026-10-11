using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Simulation.GameEntities;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// Checks the palette and climate before a build, so every key a pass looks up resolves and nothing is skipped
    /// quietly.
    /// </summary>
    public static class MapAssetCheck
    {
        private const int MaxLayers = 8;

        public static void Check(MapProject project, MapIssues issues)
        {
            CheckClimate(project.Climate, issues);
            CheckPalette(project.Palette, issues);
        }

        private static void CheckClimate(MapClimate climate, MapIssues issues)
        {
            var name = $"Climate '{climate.name}'";
            if (climate.layers.Count == 0 || climate.layers.Count > MaxLayers)
            {
                issues.Error($"{name} has {climate.layers.Count} layers; use 1 to {MaxLayers}.");
            }

            foreach (var layer in climate.layers)
            {
                if (layer.terrainLayer == null)
                {
                    issues.Error($"{name}: layer '{layer.key}' has no Terrain Layer.");
                }

                foreach (var detail in layer.details)
                {
                    Require(climate.DetailIndex(detail.detail) >= 0, $"{name}: layer '{layer.key}' grows unknown detail '{detail.detail}'.", issues);
                }
            }

            foreach (var surface in climate.surfaces)
            {
                Require(climate.LayerIndex(surface.layer) >= 0, $"{name}: surface '{surface.key}' paints unknown layer '{surface.layer}'.", issues);
                foreach (var tree in surface.trees)
                {
                    Require(climate.TreeIndex(tree.tree) >= 0, $"{name}: surface '{surface.key}' grows unknown tree '{tree.tree}'.", issues);
                }
            }

            foreach (var tree in climate.trees)
            {
                Require(tree.prefab != null, $"{name}: tree '{tree.key}' has no prefab.", issues);
            }

            foreach (var detail in climate.details)
            {
                Require(detail.mesh != null || detail.texture != null, $"{name}: detail '{detail.key}' has neither mesh nor texture.", issues);
            }

            Require(climate.water != null, $"{name} has no water material.", issues);
            Require(climate.atmosphere != null, $"{name} has no atmosphere prefab.", issues);
        }

        private static void CheckPalette(MapPalette palette, MapIssues issues)
        {
            Require(palette.rig != null, "The map palette has no camera and HUD rig.", issues);
            foreach (var entry in palette.entries)
            {
                if (entry.prefab == null)
                {
                    issues.Error($"Palette entry '{entry.key}' has no prefab.");
                    continue;
                }

                foreach (var entity in entry.prefab.GetComponentsInChildren<GameEntityAuthoring>(true))
                {
                    Require(!string.IsNullOrEmpty(entity.displayName),
                        $"Palette prefab '{entry.prefab.name}' needs a Display Name: generated objects are renamed.", issues);
                }
            }
        }

        private static void Require(bool condition, string message, MapIssues issues)
        {
            if (!condition)
            {
                issues.Error(message);
            }
        }
    }
}
