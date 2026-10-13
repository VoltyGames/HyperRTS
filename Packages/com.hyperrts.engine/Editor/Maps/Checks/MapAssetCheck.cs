using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Texturing;
using HyperRTS.Simulation.GameEntities;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>Checks before a build that every asset and key the climate and palette name exists.</summary>
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
                Require(tree.prefabs.Count > 0, $"{name}: tree '{tree.key}' has no prefabs.");
            }

            foreach (var detail in Climate.details)
            {
                Require(detail.mesh != null || detail.texture != null, $"{name}: detail '{detail.key}' has neither mesh nor texture.");
            }

            Climate.rowCrops.ForEach(crop => RowCropAssets(crop, name));
            Climate.decals.ForEach(decal => DecalAssets(decal, name));
            Require(Climate.water != null, $"{name} has no water material.");
            Require(Climate.atmosphere != null, $"{name} has no atmosphere prefab.");
            Require(Climate.fordShallowsWidth > 0f, $"{name}: fordShallowsWidth {Climate.fordShallowsWidth} must be above 0.");
            Require(Climate.fordRapidsLength > 0f, $"{name}: fordRapidsLength {Climate.fordRapidsLength} must be above 0.");
        }

        private void RowCropAssets(MapClimateRowCrop rowCrop, string name)
        {
            var what = $"{name}: row crop '{rowCrop.key}'";
            if (rowCrop.crop == null)
            {
                Issues.Error($"{what} has no crop asset.");
                return;
            }

            foreach (var problem in rowCrop.crop.Problems())
            {
                Issues.Error($"{what}: {problem}.");
            }
        }

        private void DecalAssets(MapClimateDecal decal, string name)
        {
            var what = $"{name}: decal '{decal.key}'";
            Require(decal.material != null, $"{what} has no material.");
            var sized = decal.size.x > 0f && decal.size.y >= decal.size.x;
            Require(sized, $"{what}: size {decal.size} must be above 0 with y at least x.");
            Require(decal.depth > 0f, $"{what}: depth {decal.depth} must be above 0.");
            var twoTurns = decal.alongRoad && decal.randomRotation;
            Require(!twoTurns, $"{what} sets both alongRoad and randomRotation; turn randomRotation off.");
        }

        private void Decal(string key, string source) => InClimate(Climate.decals, key, "decal", source);

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
            RavineTrees(name);
        }

        /// <summary>Ravine woods scatter the ravine surface's trees along the floor, ignoring maxEdgeDistance.</summary>
        private void RavineTrees(string name)
        {
            if (!MapKeys.Contains(Climate.surfaces, Climate.ravineSurface))
            {
                return;
            }

            var source = $"{name} ravine surface '{Climate.ravineSurface}'";
            Climate.Surface(Climate.ravineSurface).trees.ForEach(tree => NoMaxEdgeDistance(tree, source));
        }

        /// <summary>maxEdgeDistance is honoured only by a climate surface's own trees, not by kits that scatter a mix.</summary>
        private void NoMaxEdgeDistance(MapTreeDensity density, string source) =>
            Require(density.maxEdgeDistance <= 0f,
                $"{source}: tree '{density.tree}' sets maxEdgeDistance {density.maxEdgeDistance}, which only a climate surface's trees honour.");

        private void SurfaceContents(MapClimateSurface surface)
        {
            var name = $"surface '{surface.key}'";
            InClimate(Climate.layers, surface.layer, "layer", name);
            surface.trees.ForEach(tree => Tree(tree.tree, name));
            surface.details.ForEach(detail => InClimate(Climate.details, detail.detail, "detail", name));
            surface.props.ForEach(prop => EntryNotBesideRoad(prop.kind, name));
            surface.decals.ForEach(decal => Decal(decal.decal, name));
            SurfaceEdge(surface, name);
        }

        /// <summary>Every surface states its edge; a strip needs both its surface and a width.</summary>
        private void SurfaceEdge(MapClimateSurface surface, string name)
        {
            Require(surface.edgeWidth > 0f, $"{name}: edgeWidth {surface.edgeWidth} must be above 0 metres.");
            Require(surface.edgeJitter > 0f, $"{name}: edgeJitter {surface.edgeJitter} must be above 0 metres.");
            var hasStrip = !string.IsNullOrEmpty(surface.edgeSurface);
            if (!hasStrip)
            {
                Require(surface.edgeSurfaceWidth <= 0f, $"{name}: edgeSurfaceWidth {surface.edgeSurfaceWidth} needs edgeSurface set; use 0 without one.");
                return;
            }

            Surface(surface.edgeSurface, $"{name} edgeSurface");
            Require(surface.edgeSurface != surface.key, $"{name}: edgeSurface '{surface.edgeSurface}' must be another surface.");
            Require(surface.edgeSurfaceWidth > 0f, $"{name}: edgeSurfaceWidth {surface.edgeSurfaceWidth} must be above 0 metres for edgeSurface '{surface.edgeSurface}'.");
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
            Require(kit.trackJoinDistance > 0f, $"{name}: trackJoinDistance {kit.trackJoinDistance} must be above 0.");
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
            foreach (var tree in kit.trees.Concat(kit.edgeTrees))
            {
                Tree(tree.tree, name);
                NoMaxEdgeDistance(tree, name);
            }

            Surface(kit.surface, name);
            Surface(kit.edgeSurface, name);
        }

        private void EntryKeys(MapPaletteEntry entry)
        {
            var name = $"palette entry '{entry.key}'";
            Optional(entry.pad, key => Surface(key, $"{name} pad"));
            Optional(entry.access, key => InClimate(Climate.roadClasses, key, "road class", $"{name} access"));
            SharedAccessDistance(entry, name);
            Optional(entry.groundDecal, key =>
            {
                Decal(key, $"{name} ground decal");
                Require(entry.groundDecalSize > 0f,
                    $"{name}: groundDecalSize {entry.groundDecalSize} must be above 0 for groundDecal '{key}'.");
            });
        }

        /// <summary>A shared access distance only matters for props with an access track.</summary>
        private void SharedAccessDistance(MapPaletteEntry entry, string name)
        {
            var distance = entry.sharedAccessDistance;
            Require(distance >= 0f, $"{name}: sharedAccessDistance {distance} must be at least 0.");
            var hasAccess = !string.IsNullOrEmpty(entry.access);
            Require(hasAccess || distance <= 0f, $"{name}: sharedAccessDistance {distance} needs access set; use 0 without an access track.");
        }

        /// <summary>A surface or kit entry must exist and must not ask for besideRoad, which only scatters use.</summary>
        private void EntryNotBesideRoad(string key, string source)
        {
            Entry(key, source);
            NotBesideRoad(key, source);
        }

        private void SettlementKit(MapSettlementKit kit)
        {
            var name = $"settlement kit '{kit.key}'";
            Require(kit.houses.Count > 0, $"{name} has no houses.");
            Require(kit.plot.x >= 1f && kit.plot.y >= 1f, $"{name}: plot {kit.plot} must be at least 1 m each way.");
            kit.houses.ForEach(key => EntryNotBesideRoad(key, name));
            kit.outbuildings.ForEach(key => EntryNotBesideRoad(key, name));
            kit.extras.ForEach(extra => EntryNotBesideRoad(extra.kind, name));
            foreach (var key in new[] { kit.frontFence, kit.sideFence, kit.gate })
            {
                Optional(key, entry => EntryNotBesideRoad(entry, name));
            }

            Optional(kit.streetPipe, key => StreetPipe(kit, key, name));

            Optional(kit.streetPole, key =>
            {
                EntryNotBesideRoad(key, name);
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

        /// <summary>A street pipe needs its arch, a minimum section count and the widest gap an arch bridges.</summary>
        private void StreetPipe(MapSettlementKit kit, string pipe, string name)
        {
            EntryNotBesideRoad(pipe, name);
            var hasArch = !string.IsNullOrEmpty(kit.streetPipeArch);
            Require(hasArch,
                $"{name}: streetPipe '{pipe}' needs streetPipeArch, the arch that carries it over driveways and roads.");
            if (hasArch)
            {
                EntryNotBesideRoad(kit.streetPipeArch, name);
            }

            Require(kit.streetPipeMinSections >= 1, $"{name}: streetPipeMinSections {kit.streetPipeMinSections} must be at least 1 section.");
            Require(kit.streetPipeMaxGap > 0f, $"{name}: streetPipeMaxGap {kit.streetPipeMaxGap} must be above 0.");
        }
    }
}
