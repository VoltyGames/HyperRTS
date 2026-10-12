using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Farmland
{
    /// <summary>
    /// Farmland: land nothing else took, split into fields on a grid. Field ends get a shelterbelt with a dirt track
    /// beside it; each field gets a crop from the kit's mix and a weedy margin. Everything is decided in the authored
    /// sector, so every copy of a field grows the same crop.
    /// </summary>
    public static class MapFarmlandPlanner
    {
        private const float SampleStep = 1f;
        private const float MinRun = 15f;

        public static void Plan(MapPlanContext context)
        {
            var authored = context.Layout.Farmland.Where(f => f.Copy == 0).ToList();
            for (var i = 0; i < authored.Count; i++)
            {
                var spec = authored[i].Authored;
                var kit = context.Climate.FarmlandKit(spec.kind);
                if (i == 0)
                {
                    context.RowAngle = spec.angle;
                }

                var grid = new MapFieldGrid(kit, spec.angle);
                var polygon = spec.points.Count > 0 ? MapGeometry.ToVectors(spec.points) : null;
                LayEnds(context, kit, grid, s => IsFarmland(context, polygon, s), i);
                MapFieldClaims.Claim(context, kit, grid, polygon, i, spec.angle);
            }
        }

        /// <summary>A sector point the farmland may still use.</summary>
        public static bool IsFarmland(MapPlanContext context, List<Vector2> polygon, Vector2 sectorPoint)
        {
            if (!context.Layout.Symmetry.InSector(sectorPoint))
            {
                return false;
            }

            if (polygon != null && !MapGeometry.Contains(polygon, sectorPoint))
            {
                return false;
            }

            var land = context.Land;
            return land.TryCell(sectorPoint, out var x, out var z) && land.IsFree(x, z);
        }

        private static void LayEnds(MapPlanContext context, MapFarmlandKit kit, MapFieldGrid grid,
            Func<Vector2, bool> farmland, int index)
        {
            var belt = string.IsNullOrEmpty(kit.beltKit) ? null : context.Climate.BeltKit(kit.beltKit);
            var track = string.IsNullOrEmpty(kit.trackClass) ? null : context.Climate.RoadClass(kit.trackClass);
            var area = context.Land.Bounds;
            var across = grid.Across(area);
            var (first, last) = grid.EndLines(area);
            var beltRuns = new List<(List<Vector2> Run, int Line)>();
            var trackRuns = new List<List<Vector2>>();
            for (var line = first; line <= last; line++)
            {
                var hasBelt = belt != null && Mod(line, kit.beltEvery) == 0;
                var u = grid.EndLine(line);
                if (hasBelt)
                {
                    beltRuns.AddRange(Runs(grid, u, across, farmland).Select(r => (r, line)));
                }

                if (track != null)
                {
                    var offset = hasBelt ? belt.width * 0.5f + track.width * 0.5f + 0.5f : 0f;
                    trackRuns.AddRange(Runs(grid, u + offset, across, farmland));
                }
            }

            foreach (var run in trackRuns)
            {
                MapTrackLayer.Lay(context, track, run);
            }

            foreach (var (run, line) in beltRuns)
            {
                MapBeltPlanner.PlanLine(context, belt, run, index * 1000 + line);
            }
        }

        /// <summary>Stretches of the line u = const where the farmland is free, as sector polylines.</summary>
        private static IEnumerable<List<Vector2>> Runs(MapFieldGrid grid, float u, Vector2 across, Func<Vector2, bool> farmland)
        {
            var run = new List<Vector2>();
            for (var v = across.x; v <= across.y; v += SampleStep)
            {
                var point = grid.FromGrid(u, v);
                if (farmland(point))
                {
                    run.Add(point);
                    continue;
                }

                if (run.Count * SampleStep >= MinRun)
                {
                    yield return Simplify(run);
                }

                run = new List<Vector2>();
            }

            if (run.Count * SampleStep >= MinRun)
            {
                yield return Simplify(run);
            }
        }

        /// <summary>A straight run sampled every metre needs only its ends.</summary>
        private static List<Vector2> Simplify(List<Vector2> run) => new() { run[0], run[^1] };

        private static int Mod(int value, int every) => ((value % every) + every) % every;
    }
}
