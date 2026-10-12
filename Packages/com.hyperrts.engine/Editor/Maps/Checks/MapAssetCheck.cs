using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Texturing;
using HyperRTS.Simulation.GameEntities;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>
    /// Checks the climate and palette before a build: every asset a pass needs is assigned, and every surface, tree,
    /// plant, road class, kit and palette key their kits name exists.
    /// </summary>
    public sealed class MapAssetCheck : MapKeyCheck
    {
        private MapAssetCheck(MapClimate climate, MapPalette palette, MapIssues issues) : base(climate, palette, issues)
        {
        }

        public static void Check(MapClimate climate, MapPalette palette, MapIssues issues) =>
            new MapAssetCheck(climate, palette, issues).Run();

        private void Run()
        {
            ClimateAssets();
            PaletteAssets();
            ClimateKeys();
            Climate.surfaces.ForEach(SurfaceContents);
            Climate.roadClasses.ForEach(RoadClass);
            Climate.farmlandKits.ForEach(FarmlandKit);
            Climate.beltKits.ForEach(BeltKit);
            Climate.woodlandKits.ForEach(WoodlandKit);
            Palette.entries.ForEach(EntryKeys);
            Palette.startKit.ForEach(item => Entry(item.key, "start kit"));
            Palette.settlementKits.ForEach(SettlementKit);
        }

        private void ClimateAssets()
        {
            var name = $"Climate '{Climate.name}'";
            foreach (var problem in MapTerrainTextures.Problems(Climate))
            {
                Issues.Error($"{name}: {problem}");
            }

            foreach (var tree in Climate.trees)
            {
                Require(tree.prefab != null, $"{name}: tree '{tree.key}' has no prefab.");
            }

            foreach (var detail in Climate.details)
            {
                Require(detail.mesh != null || detail.texture != null, $"{name}: detail '{detail.key}' has neither mesh nor texture.");
            }

            Require(Climate.water != null, $"{name} has no water material.");
            Require(Climate.atmosphere != null, $"{name} has no atmosphere prefab.");
        }

        private void PaletteAssets()
        {
            Require(Palette.rig != null, "The map palette has no camera and HUD rig.");
            foreach (var entry in Palette.entries)
            {
                if (entry.prefab == null)
                {
                    Issues.Error($"Palette entry '{entry.key}' has no prefab.");
                    continue;
                }

                foreach (var entity in entry.prefab.GetComponentsInChildren<GameEntityAuthoring>(true))
                {
                    Require(!string.IsNullOrEmpty(entity.displayName),
                        $"Palette prefab '{entry.prefab.name}' needs a Display Name: generated objects are renamed.");
                }
            }
        }

        private void ClimateKeys()
        {
            var name = $"Climate '{Climate.name}'";
            Surface(Climate.groundSurface, $"{name} ground");
            Surface(Climate.groundVariation, $"{name} ground variation");
            Surface(Climate.baseSurface, $"{name} base");
            Surface(Climate.slopeSurface, $"{name} slope");
            Surface(Climate.shoreSurface, $"{name} shore");
            Surface(Climate.floodplainSurface, $"{name} floodplain");
            Surface(Climate.ravineSurface, $"{name} ravine");
            Surface(Climate.ravineRimSurface, $"{name} ravine rim");
            Optional(Climate.reedDetail, key => InClimate(Climate.details, key, "detail", $"{name} reeds"));
        }

        private void SurfaceContents(MapClimateSurface surface)
        {
            var name = $"surface '{surface.key}'";
            InClimate(Climate.layers, surface.layer, "layer", name);
            surface.trees.ForEach(tree => Tree(tree.tree, name));
            surface.details.ForEach(detail => InClimate(Climate.details, detail.detail, "detail", name));
            surface.props.ForEach(prop => Entry(prop.kind, name));
        }

        private void RoadClass(MapRoadClass road)
        {
            var name = $"road class '{road.key}'";
            Surface(road.surface, name);
            if (road.vergeWidth > 0f)
            {
                Surface(road.vergeSurface, $"{name} verge");
            }
        }

        private void FarmlandKit(MapFarmlandKit kit)
        {
            var name = $"farmland kit '{kit.key}'";
            Require(kit.crops.Count > 0, $"{name} has no crops.");
            kit.crops.ForEach(crop => Surface(crop.surface, name));
            kit.leftovers.ForEach(leftover => Surface(leftover.surface, name));
            Surface(kit.marginSurface, name);
            Optional(kit.beltKit, key => InClimate(Climate.beltKits, key, "belt kit", name));
            Optional(kit.trackClass, key => InClimate(Climate.roadClasses, key, "road class", $"{name} tracks"));
        }

        private void BeltKit(MapBeltKit kit)
        {
            var name = $"belt kit '{kit.key}'";
            Require(kit.trees.Count > 0, $"{name} has no trees.");
            kit.trees.ForEach(tree => Tree(tree.tree, name));
            Optional(kit.shrub, key => Tree(key, $"{name} shrub"));
            Surface(kit.surface, name);
        }

        private void WoodlandKit(MapWoodlandKit kit)
        {
            var name = $"woodland kit '{kit.key}'";
            kit.trees.ForEach(tree => Tree(tree.tree, name));
            kit.edgeTrees.ForEach(tree => Tree(tree.tree, name));
            Surface(kit.surface, name);
            Surface(kit.edgeSurface, name);
        }

        private void EntryKeys(MapPaletteEntry entry)
        {
            var name = $"palette entry '{entry.key}'";
            Optional(entry.pad, key => Surface(key, $"{name} pad"));
            Optional(entry.access, key => InClimate(Climate.roadClasses, key, "road class", $"{name} access"));
        }

        private void SettlementKit(MapSettlementKit kit)
        {
            var name = $"settlement kit '{kit.key}'";
            Require(kit.houses.Count > 0, $"{name} has no houses.");
            Require(kit.plot.x >= 1f && kit.plot.y >= 1f, $"{name}: plot {kit.plot} must be at least 1 m each way.");
            kit.houses.ForEach(key => Entry(key, name));
            kit.outbuildings.ForEach(key => Entry(key, name));
            kit.extras.ForEach(extra => Entry(extra.kind, name));
            foreach (var key in new[] { kit.frontFence, kit.sideFence, kit.gate, kit.streetPipe })
            {
                Optional(key, entry => Entry(entry, name));
            }

            Optional(kit.streetPole, key =>
            {
                Entry(key, name);
                Require(kit.poleSpacing >= 1f, $"{name}: poleSpacing {kit.poleSpacing} must be at least 1 m.");
            });
            Optional(kit.orchardTree, key =>
            {
                Tree(key, $"{name} orchard");
                Require(kit.orchardSpacing >= 1f, $"{name}: orchardSpacing {kit.orchardSpacing} must be at least 1 m.");
            });
            Surface(kit.yardSurface, name);
            Surface(kit.gardenSurface, name);
            Surface(kit.backSurface, name);
        }
    }
}
