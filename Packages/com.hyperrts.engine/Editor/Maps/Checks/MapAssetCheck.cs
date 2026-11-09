using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Kits;
using HyperRTS.Simulation.Common.Authoring;
using System.Linq;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;
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
            MapFieldCheck.CheckLibrary(Climate, Palette, Issues);
            RavineTrees();
            Climate.surfaces.ForEach(SurfaceContents);
            Climate.roadClasses.ForEach(RoadClass);
            Climate.farmlandKits.ForEach(FarmlandKit);
            Climate.beltKits.ForEach(BeltKit);
            Climate.woodlandKits.ForEach(WoodlandKit);
            Palette.entries.ForEach(EntryKeys);
            Palette.entries.ForEach(entry => MapStretchPartsCheck.Check(entry, Issues));
            new MapSideCheck(Climate, Palette, Issues).Check();
            Palette.settlementKits.ForEach(SettlementKit);
        }

        private void ClimateAssets()
        {
            var name = $"Climate '{Climate.key}'";
            foreach (var problem in MapTerrainTextures.Problems(Climate))
            {
                Issues.Error($"{name}: {problem}");
            }

            foreach (var tree in Climate.trees)
            {
                Require(tree.prefabs.Count > 0, $"{name}: tree '{tree.key}' has no prefabs.");
            }

            foreach (var plant in Climate.plants)
            {
                foreach (var problem in plant.Plant().Problems())
                {
                    Issues.Error($"{name}: plant '{plant.key}': {problem}.");
                }
            }

            Climate.rowCrops.ForEach(crop => RowCropAssets(crop, name));
            Climate.decals.ForEach(decal => DecalAssets(decal, name));
            Require(Climate.terrain != null, $"{name} has no terrain material.");
            Require(Climate.water != null, $"{name} has no water material.");
            MapWeatherCheck.Check(Climate, Issues);
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
            var twoTurns = decal.alongRoad && decal.randomRotation;
            Require(!twoTurns, $"{what} sets both alongRoad and randomRotation; turn randomRotation off.");
        }

        private void PaletteAssets()
        {
            Require(Palette.rig != null, "The map palette has no camera and HUD rig.");
            foreach (var entry in Palette.entries)
            {
                if (entry.prefab == null)
                {
                    Issues.Error($"Prop '{entry.key}' has no prefab.");
                    continue;
                }

                foreach (var entity in entry.prefab.GetComponentsInChildren<GameEntityAuthoring>(true))
                {
                    Require(!string.IsNullOrEmpty(entity.displayName),
                        $"Palette prefab '{entry.prefab.name}' needs a Display Name: generated objects are renamed.");
                }

                var backdrop = entry.backdropModel;
                if (backdrop != null && backdrop.GetComponentInChildren<AuthoringBehaviour>(true) != null)
                {
                    Require(false, $"Prop '{entry.key}': backdrop model '{backdrop.name}' must be a plain model, not an entity prefab.");
                }
            }
        }

        /// <summary>Ravine woods scatter the ravine surface's trees along the floor, ignoring maxEdgeDistance.</summary>
        private void RavineTrees()
        {
            if (!MapKeys.Contains(Climate.surfaces, Climate.ravineSurface))
            {
                return;
            }

            var source = $"Climate '{Climate.key}' ravine surface '{Climate.ravineSurface}'";
            Climate.Surface(Climate.ravineSurface).trees.ForEach(tree => NoMaxEdgeDistance(tree, source));
        }

        /// <summary>maxEdgeDistance is honoured only by a climate surface's own trees, not by kits that scatter a mix.</summary>
        private void NoMaxEdgeDistance(MapTreeDensity density, string source) =>
            Require(density.maxEdgeDistance <= 0f,
                $"{source}: tree '{density.tree}' sets maxEdgeDistance {density.maxEdgeDistance}, which only a climate surface's trees honour.");

        private void SurfaceContents(MapClimateSurface surface)
        {
            var name = $"surface '{surface.key}'";
            surface.props.ForEach(prop => NotBesideRoad(prop.prop, name));
            surface.mixes.ForEach(mix => SurfaceMix(mix, name));
            Require(surface.tramlines.Valid && surface.swaths.Valid,
                $"{name}: tramlines need a layer, a width above 0 and gauge plus width under spacing; swaths a width from 0 to spacing.");
            Require(MapImpactClass.IsValid(surface.impactClass), MapImpactClass.Problem(name, surface.impactClass));
        }

        private void SurfaceMix(MapSurfaceMix mix, string name)
        {
            Require(mix.Valid, $"{name}: mix '{mix.layer}' coverage {mix.coverage} and alongFlow {mix.alongFlow} must be in 0..1 and size {mix.size} above 0.");
        }

        private void RoadClass(MapRoadClass road)
        {
            var name = $"road class '{road.key}'";
            Require(MapImpactClass.IsValid(road.impactClass), MapImpactClass.Problem(name, road.impactClass));
            Require(road.vergeWidth <= 0f || !string.IsNullOrEmpty(road.vergeSurface), $"{name}: vergeWidth {road.vergeWidth} needs a vergeSurface.");
        }

        private void FarmlandKit(MapFarmlandKit kit)
        {
            var name = $"farmland kit '{kit.key}'";
            Require(kit.crops.Count > 0, $"{name} has no crops.");
        }

        private void BeltKit(MapBeltKit kit)
        {
            var name = $"belt kit '{kit.key}'";
            Require(kit.trees.Count > 0, $"{name} has no trees.");
        }

        private void WoodlandKit(MapWoodlandKit kit)
        {
            var name = $"woodland kit '{kit.key}'";
            foreach (var tree in kit.trees.Concat(kit.edgeTrees))
            {
                NoMaxEdgeDistance(tree, name);
            }
        }

        private void EntryKeys(MapProp entry)
        {
            var name = $"prop '{entry.key}'";
            SharedAccessDistance(entry, name);
            Optional(entry.groundDecal, key =>
                Require(entry.groundDecalSize > 0f,
                    $"{name}: groundDecalSize {entry.groundDecalSize} must be above 0 for groundDecal '{key}'."));
        }

        /// <summary>A shared access distance only matters for props with an access track.</summary>
        private void SharedAccessDistance(MapProp entry, string name)
        {
            var distance = entry.sharedAccessDistance;
            var hasAccess = !string.IsNullOrEmpty(entry.access);
            Require(hasAccess || distance <= 0f, $"{name}: sharedAccessDistance {distance} needs access set; use 0 without an access track.");
        }

        private void SettlementKit(MapSettlementKit kit)
        {
            var name = $"settlement kit '{kit.key}'";
            Require(kit.houses.Count > 0, $"{name} has no houses.");
            Require(kit.plot.x >= 1f && kit.plot.y >= 1f, $"{name}: plot {kit.plot} must be at least 1 m each way.");
            kit.houses.ForEach(key => NotBesideRoad(key, name));
            kit.outbuildings.ForEach(key => NotBesideRoad(key, name));
            kit.extras.ForEach(extra => NotBesideRoad(extra.kind, name));
            foreach (var key in new[] { kit.frontFence, kit.sideFence, kit.gate })
            {
                Optional(key, entry => NotBesideRoad(entry, name));
            }

            Optional(kit.streetPipe, key => StreetPipe(kit, key, name));

            Optional(kit.streetPole, key =>
            {
                NotBesideRoad(key, name);
                Require(kit.poleSpacing >= 1f, $"{name}: poleSpacing {kit.poleSpacing} must be at least 1 m.");
            });
            Require(string.IsNullOrEmpty(kit.orchardTree) || kit.orchardSpacing >= 1f,
                $"{name}: orchardSpacing {kit.orchardSpacing} must be at least 1 m.");
        }

        /// <summary>A street pipe needs its arch, a minimum section count and the widest gap an arch bridges.</summary>
        private void StreetPipe(MapSettlementKit kit, string pipe, string name)
        {
            NotBesideRoad(pipe, name);
            var hasArch = !string.IsNullOrEmpty(kit.streetPipeArch);
            Require(hasArch,
                $"{name}: streetPipe '{pipe}' needs streetPipeArch, the arch that carries it over driveways and roads.");
            if (hasArch)
            {
                NotBesideRoad(kit.streetPipeArch, name);
            }

            Require(kit.streetPipeMinSections >= 1, $"{name}: streetPipeMinSections {kit.streetPipeMinSections} must be at least 1 section.");
            Require(kit.streetPipeMaxGap > 0f, $"{name}: streetPipeMaxGap {kit.streetPipeMaxGap} must be above 0.");
        }
    }
}
