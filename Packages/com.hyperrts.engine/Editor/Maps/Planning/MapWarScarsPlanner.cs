using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Scars the land for the spec's war age from the climate's war kit: shredded belt and wood-edge trees, wrecks
    /// near cover, trenches along belts, old craters dug into the ground and burnt patches. Chosen in the authored
    /// sector and copied, on open land clear of roads, bases, pads, resources and settlements.
    /// </summary>
    public sealed class MapWarScarsPlanner : MapLandStep
    {
        private const int Attempts = 10;
        private const float TreeClearance = 0.5f;
        private const int WreckStream = 0;
        private const int TrenchStream = 1;
        private const int CraterStream = 2;
        private const int BurnStream = 3;

        private MapWarKit _kit;
        private MapWarDensity _density;
        private List<int> _open;
        private float _hectares;

        /// <summary>Distance in cells from every land cell to the nearest belt or wood.</summary>
        private float[] _cover;

        public MapWarScarsPlanner(MapPlanContext context) : base(context)
        {
        }

        private MapClearance Clearance => Context.Placements.Clearance;

        public override void Plan()
        {
            var age = Context.Layout.Spec.WarAge;
            if (age == MapWarAge.None)
            {
                return;
            }

            _kit = Climate.war;
            _density = _kit.Density(age);
            _open = OpenCells();
            _hectares = Hectares(_open.Count);
            _cover = MapLandDistance.To(Land.Width, Land.Height, (x, z) => IsCover(Land.Region(x, z)));
            ShredTrees();
            PlaceWrecks();
            DigTrenches();
            DigCraters();
            BurnPatches();
        }

        /// <summary>Sector cells of fields, margins and meadows without row crops, which would stand through a scar.</summary>
        private List<int> OpenCells()
        {
            var open = new List<int>();
            foreach (var (region, cells) in SectorCells(surface => string.IsNullOrEmpty(surface.rowCrop), playableOnly: true))
            {
                if (Land.Regions[region].Kind is MapLandKind.Field or MapLandKind.Margin or MapLandKind.Meadow)
                {
                    open.AddRange(cells);
                }
            }

            return open;
        }

        private void ShredTrees()
        {
            var planted = Context.Trees.Planted;
            for (var i = 0; i < planted.Count; i++)
            {
                var tree = planted[i];
                var swap = _kit.Swap(Climate.trees[tree.Species].key);
                var sectorPoint = Symmetry.ToSector(tree.Position, out _);
                if (swap == null || swap.shredded.Count == 0 || !IsShredded(sectorPoint))
                {
                    continue;
                }

                var seed = MapTree.SeedAt(sectorPoint);
                var species = Climate.TreeIndex(swap.shredded[(int)(seed % (uint)swap.shredded.Count)]);
                planted[i] = new MapTree(species, tree.Position, tree.Scale, tree.Yaw, Climate.trees[species].Variant(seed));
            }
        }

        /// <summary>Decided at the sector point, so every copy of a tree agrees.</summary>
        private bool IsShredded(Vector2 sectorPoint)
        {
            var edgeTree = Land.KindAt(sectorPoint) is MapLandKind.Belt or MapLandKind.WoodEdge;
            var roll = ((MapTree.SeedAt(sectorPoint) >> 16) ^ ((uint)Context.Seed & 0xFFFFu)) / 65536f;
            return edgeTree && roll < _density.shreddedShare;
        }

        /// <summary>Wrecks lie close to belts and woods, so the open lanes units cross stay as they were.</summary>
        private void PlaceWrecks()
        {
            var near = _open.FindAll(cell => _cover[cell] * MapLand.CellSize <= _kit.wreckCoverDistance);
            if (_kit.wrecks.Count == 0 || near.Count == 0)
            {
                return;
            }

            var random = Context.Random(MapRandomChannel.WarScars, WreckStream);
            var wanted = random.Count(_density.wrecksPerHectare * _hectares);
            var placed = 0;
            for (var attempt = 0; attempt < wanted * Attempts && placed < wanted; attempt++)
            {
                var entry = Context.Palette.Entry(_kit.wrecks[random.Next(_kit.wrecks.Count)]);
                placed += TryPlace(entry, RandomSpot(near, random), random.Yaw(), random) ? 1 : 0;
            }
        }

        private void DigTrenches()
        {
            var band = _open.FindAll(cell => InTrenchBand(cell, MapLand.CellSize));
            if (_kit.trenchProps.Count == 0 || band.Count == 0)
            {
                return;
            }

            var random = Context.Random(MapRandomChannel.WarScars, TrenchStream);
            var wanted = random.Count(_density.trenchesPerHectare * _hectares);
            for (var i = 0; i < wanted; i++)
            {
                LayTrench(RandomSpot(band, random), random);
            }
        }

        /// <summary>Lays pieces end to end along the belt's edge until the length is reached or something is in the way.</summary>
        private void LayTrench(Vector2 start, System.Random random)
        {
            var length = random.Range(_kit.trenchLength.x, _kit.trenchLength.y);
            var at = start;
            var heading = Vector2.zero;
            for (var laid = 0f; laid < length;)
            {
                var entry = Context.Palette.Entry(_kit.trenchProps[random.Next(_kit.trenchProps.Count)]);
                if (!Land.TryCell(at, out var x, out var z) || !InTrenchBand(z * Land.Width + x, MapLand.CellSize * 1.5f))
                {
                    return;
                }

                heading = AlongCover(x, z, heading);
                if (heading == Vector2.zero || !TryPlace(entry, at, MapGeometry.Yaw(heading), random))
                {
                    return;
                }

                var step = Mathf.Max(entry.size.x, entry.size.y);
                at += heading * step;
                laid += step;
            }
        }

        private bool InTrenchBand(int cell, float tolerance) =>
            Mathf.Abs(_cover[cell] * MapLand.CellSize - _kit.trenchOffset) < tolerance;

        /// <summary>The direction along the nearest cover's edge, kept pointing the way <paramref name="heading"/> went.</summary>
        private Vector2 AlongCover(int x, int z, Vector2 heading)
        {
            var east = _cover[z * Land.Width + Mathf.Min(x + 1, Land.Width - 1)];
            var west = _cover[z * Land.Width + Mathf.Max(x - 1, 0)];
            var north = _cover[Mathf.Min(z + 1, Land.Height - 1) * Land.Width + x];
            var south = _cover[Mathf.Max(z - 1, 0) * Land.Width + x];
            var gradient = new Vector2(east - west, north - south);
            if (gradient == Vector2.zero)
            {
                return Vector2.zero;
            }

            var along = MapGeometry.Right(gradient.normalized);
            return Vector2.Dot(along, heading) < 0f ? -along : along;
        }

        private bool TryPlace(MapProp entry, Vector2 spot, float yaw, System.Random random)
        {
            var scale = random.Range(entry.scale.x, entry.scale.y);
            var radius = entry.Radius * scale;
            if (!InSector(spot, radius) || !Clearance.IsOffRoad(spot, radius) || !Clearance.IsFree(spot, radius))
            {
                return false;
            }

            if (!Context.Trees.Fits(spot, radius + TreeClearance))
            {
                return false;
            }

            Context.Placements.AddEverywhere(entry, spot, yaw, scale, 1f);
            return true;
        }

        private void DigCraters()
        {
            var random = Context.Random(MapRandomChannel.WarScars, CraterStream);
            var wanted = random.Count(_density.cratersPerHectare * _hectares);
            var dug = new List<MapWarCrater>();
            for (var attempt = 0; attempt < wanted * Attempts && dug.Count < wanted; attempt++)
            {
                var spot = RandomSpot(_open, random);
                var crater = new MapWarCrater(spot, random.Range(_kit.craterRadius.x, _kit.craterRadius.y),
                    random.Range(_kit.craterDepth.x, _kit.craterDepth.y));
                var yaw = random.Yaw();
                if (CraterFits(crater, dug))
                {
                    dug.Add(crater);
                    DigCopies(crater, yaw);
                }
            }
        }

        private bool CraterFits(MapWarCrater crater, List<MapWarCrater> dug)
        {
            var spot = crater.Center;
            var reach = crater.Reach;
            if (!InSector(spot, reach) || !Clearance.IsOpenGround(spot, reach) || !Clearance.IsFree(spot, reach))
            {
                return false;
            }

            return Context.Trees.Fits(spot, crater.Radius) &&
                   dug.TrueForAll(other => Vector2.Distance(other.Center, spot) > other.Reach + reach);
        }

        private void DigCopies(MapWarCrater crater, float yaw)
        {
            var size = crater.Radius * 2f * _kit.craterDecalSize;
            var wet = crater.Depth >= _kit.wetDepth;
            foreach (var copy in Symmetry.Copies)
            {
                var center = copy.Apply(crater.Center);
                Context.WarScars.Dig(new MapWarCrater(center, crater.Radius, crater.Depth), Context.Heights);
                Context.Decals.Add(new MapDecal(Climate.Decal(_kit.craterDecal), center, size, copy.ApplyYaw(yaw), copy.Mirrored));
                if (wet)
                {
                    Context.WetDecals.Add(new MapDecal(Climate.Decal(_kit.wetDecal), center, crater.Radius, copy.ApplyYaw(yaw), copy.Mirrored));
                }
            }
        }

        private void BurnPatches()
        {
            var random = Context.Random(MapRandomChannel.WarScars, BurnStream);
            var wanted = random.Count(_density.burnsPerHectare * _hectares);
            var burnt = 0;
            for (var attempt = 0; attempt < wanted * Attempts && burnt < wanted; attempt++)
            {
                var spot = RandomSpot(_open, random);
                var patch = new MapBurntPatch(spot, random.Range(_kit.burntRadius.x, _kit.burntRadius.y));
                if (!InSector(spot, patch.Reach) || !Clearance.IsOpenGround(spot, patch.Radius * 0.5f))
                {
                    continue;
                }

                foreach (var copy in Symmetry.Copies)
                {
                    Context.WarScars.Burns.Add(new MapBurntPatch(copy.Apply(spot), patch.Radius));
                }

                burnt++;
            }
        }

        /// <summary>Inside the authored sector and far enough from its seam that copies don't overlap.</summary>
        private bool InSector(Vector2 spot, float radius) => Symmetry.InSector(spot) && Symmetry.SeamDistance(spot) > radius;

        private static bool IsCover(MapLandRegion region) =>
            region is { Kind: MapLandKind.Belt or MapLandKind.Wood or MapLandKind.WoodEdge };
    }
}
