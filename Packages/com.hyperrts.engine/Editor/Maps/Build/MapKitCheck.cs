using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Layout;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// Checks that every surface, tree, plant, road class, kit and palette key the climate's land kits and the
    /// palette's settlement kits name exists, so planners never meet an unknown key halfway through a build.
    /// </summary>
    public static class MapKitCheck
    {
        public static void Check(MapProject project, MapIssues issues)
        {
            var check = new Checker(project.Climate, project.Palette, issues);
            check.Climate();
            foreach (var kit in project.Palette.settlementKits)
            {
                check.Settlement(kit);
            }
        }

        private sealed class Checker
        {
            private readonly MapClimate _climate;
            private readonly MapPalette _palette;
            private readonly MapIssues _issues;

            public Checker(MapClimate climate, MapPalette palette, MapIssues issues)
            {
                _climate = climate;
                _palette = palette;
                _issues = issues;
            }

            public void Climate()
            {
                var name = $"Climate '{_climate.name}'";
                foreach (var key in new[] { _climate.groundSurface, _climate.groundVariation, _climate.baseSurface,
                             _climate.slopeSurface, _climate.shoreSurface, _climate.floodplainSurface,
                             _climate.ravineSurface, _climate.ravineRimSurface })
                {
                    Surface(key, name);
                }

                Optional(_climate.reedDetail, k => _climate.DetailIndex(k) >= 0, $"{name}: reed detail");
                foreach (var surface in _climate.surfaces)
                {
                    SurfaceContents(surface);
                }

                foreach (var road in _climate.roadClasses)
                {
                    Surface(road.surface, $"road class '{road.key}'");
                    Optional(road.vergeSurface, k => _climate.Surface(k) != null, $"road class '{road.key}' verge");
                }

                Kits();
            }

            public void Settlement(MapSettlementKit kit)
            {
                var name = $"settlement kit '{kit.key}'";
                Require(kit.houses.Count > 0, $"{name} has no houses.");
                Entries(kit.houses, name);
                Entries(kit.outbuildings, name);
                foreach (var key in new[] { kit.frontFence, kit.sideFence, kit.gate, kit.streetPipe, kit.streetPole })
                {
                    Optional(key, k => _palette.Entry(k) != null, name);
                }

                foreach (var extra in kit.extras)
                {
                    Require(_palette.Entry(extra.kind) != null, $"{name}: extra '{extra.kind}' is not in the palette.");
                }

                Surface(kit.yardSurface, name);
                Surface(kit.gardenSurface, name);
                Surface(kit.backSurface, name);
                Optional(kit.orchardTree, k => _climate.TreeIndex(k) >= 0, $"{name} orchard tree");
            }

            private void Kits()
            {
                foreach (var kit in _climate.farmlandKits)
                {
                    var name = $"farmland kit '{kit.key}'";
                    Require(kit.crops.Count > 0, $"{name} has no crops.");
                    kit.crops.ForEach(c => Surface(c.surface, name));
                    Surface(kit.marginSurface, name);
                    Optional(kit.beltKit, k => _climate.BeltKit(k) != null, $"{name} belt kit");
                    Optional(kit.trackClass, k => _climate.RoadClass(k) != null, $"{name} track class");
                }

                foreach (var kit in _climate.beltKits)
                {
                    var name = $"belt kit '{kit.key}'";
                    Require(kit.trees.Count > 0, $"{name} has no trees.");
                    kit.trees.ForEach(t => Tree(t.tree, name));
                    Optional(kit.shrub, k => _climate.TreeIndex(k) >= 0, $"{name} shrub");
                    Surface(kit.surface, name);
                }

                foreach (var kit in _climate.woodlandKits)
                {
                    var name = $"woodland kit '{kit.key}'";
                    kit.trees.ForEach(t => Tree(t.tree, name));
                    kit.edgeTrees.ForEach(t => Tree(t.tree, name));
                    Surface(kit.surface, name);
                    Surface(kit.edgeSurface, name);
                }
            }

            private void SurfaceContents(MapClimateSurface surface)
            {
                var name = $"surface '{surface.key}'";
                Require(_climate.LayerIndex(surface.layer) >= 0, $"{name} paints unknown layer '{surface.layer}'.");
                surface.trees.ForEach(t => Tree(t.tree, name));
                foreach (var detail in surface.details)
                {
                    Require(_climate.DetailIndex(detail.detail) >= 0, $"{name} grows unknown detail '{detail.detail}'.");
                }

                foreach (var prop in surface.props)
                {
                    Require(_palette.Entry(prop.kind) != null, $"{name} scatters '{prop.kind}', which isn't in the palette.");
                }
            }

            private void Entries(List<string> keys, string source)
            {
                foreach (var key in keys)
                {
                    Require(_palette.Entry(key) != null, $"{source}: '{key}' is not in the palette.");
                }
            }

            private void Surface(string key, string source) =>
                Require(_climate.Surface(key) != null, $"{source}: surface '{key}' is not in climate '{_climate.name}'.");

            private void Tree(string key, string source) =>
                Require(_climate.TreeIndex(key) >= 0, $"{source}: tree '{key}' is not in climate '{_climate.name}'.");

            private void Optional(string key, System.Func<string, bool> exists, string source)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    Require(exists(key), $"{source}: '{key}' doesn't exist.");
                }
            }

            private void Require(bool condition, string message)
            {
                if (!condition)
                {
                    _issues.Error(message);
                }
            }
        }
    }
}
