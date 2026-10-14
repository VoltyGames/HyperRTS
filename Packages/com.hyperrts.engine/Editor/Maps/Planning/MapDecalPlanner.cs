using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>Ground decals: each surface's per-hectare mix, laid in the authored sector and copied (wet-only ones
    /// apart), then one under each prop that names one.</summary>
    public sealed class MapDecalPlanner : MapLandPass
    {
        private const int Attempts = 10;

        /// <summary>How far an along-road decal's yaw strays from the road's heading (degrees either way).</summary>
        private const float RoadJitter = 6f;

        public MapDecalPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            LaySurfaceDecals();
            LayPropDecals();
        }

        private void LaySurfaceDecals()
        {
            foreach (var (index, cells) in SectorCells(surface => surface.decals.Count > 0, playableOnly: true))
            {
                var surface = Climate.Surface(Land.Regions[index].Surface);
                var random = Context.Random(MapRandomChannel.Decals, index);
                foreach (var density in surface.decals)
                {
                    var wanted = random.Count(density.perHectare * Hectares(cells.Count));
                    var laid = density.wet ? Context.WetDecals : Context.Decals;
                    Scatter(Climate.Decal(density.decal), wanted, cells, random, laid);
                }
            }
        }

        private void Scatter(MapClimateDecal decal, int wanted, List<int> cells, System.Random random, List<MapDecal> into)
        {
            var laid = 0;
            for (var attempt = 0; attempt < wanted * Attempts && laid < wanted; attempt++)
            {
                var spot = RandomSpot(cells, random);
                var diameter = random.Range(decal.size.x, decal.size.y);
                var yaw = decal.randomRotation ? random.Yaw() : 0f;
                if (decal.alongRoad && !OnRoad(ref spot, ref yaw, random))
                {
                    continue;
                }

                if (!Context.Placements.Clearance.IsFree(spot, diameter * 0.25f))
                {
                    continue;
                }

                LayCopies(decal, spot, diameter, yaw, into);
                laid++;
            }
        }

        /// <summary>Moves the spot onto the centre line of the road it lies on and turns it along; false off any road.</summary>
        private bool OnRoad(ref Vector2 spot, ref float yaw, System.Random random)
        {
            var jitter = random.Signed() * RoadJitter;
            if (!Context.Shapes.NearestRoad(spot, out _, out var center, out var direction, out var edge))
            {
                return false;
            }

            if (edge > MapLand.CellSize)
            {
                return false;
            }

            spot = center;
            yaw = MapGeometry.Yaw(direction) + jitter;
            return true;
        }

        private void LayCopies(MapClimateDecal decal, Vector2 sectorPoint, float diameter, float yaw, List<MapDecal> into)
        {
            foreach (var copy in Symmetry.Copies)
            {
                into.Add(new MapDecal(decal, copy.Apply(sectorPoint), diameter, copy.ApplyYaw(yaw), copy.Mirrored));
            }
        }

        /// <summary>Placements are already copied, so each gets its decal turned with it.</summary>
        private void LayPropDecals()
        {
            foreach (var placement in Context.Placements.All)
            {
                var entry = placement.Entry;
                if (string.IsNullOrEmpty(entry.groundDecal))
                {
                    continue;
                }

                var decal = Climate.Decal(entry.groundDecal);
                var diameter = entry.groundDecalSize * placement.Scale;
                Symmetry.ToSector(placement.Position, out var copy);
                var mirrored = Symmetry.Copies[copy].Mirrored;
                Context.Decals.Add(new MapDecal(decal, placement.Position, diameter, placement.Yaw, mirrored));
            }
        }
    }
}
