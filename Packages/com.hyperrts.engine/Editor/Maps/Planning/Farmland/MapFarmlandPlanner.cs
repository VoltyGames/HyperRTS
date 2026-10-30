using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Farmland
{
    /// <summary>
    /// Farmland: land nothing else took, split into fields on a grid. Field ends get a shelterbelt with a dirt track
    /// beside it; each field gets a crop from the kit's mix, a weedy margin and a headland at each end where the rows
    /// turn, and pieces too small or too narrow to farm become the kit's leftover ground. Everything is decided in the authored sector, so every copy of a field
    /// grows the same crop.
    /// </summary>
    public sealed class MapFarmlandPlanner : MapLandStep
    {
        private const float SampleStep = 1f;
        private const float MinRun = 15f;
        private const int NoField = -1;

        // Shorter stretches, in headland depths, keep straight rows.
        private const float HeadlandBody = 3f;

        public MapFarmlandPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            var authored = Context.Layout.Farmland.Where(f => f.Copy == 0).ToList();
            for (var i = 0; i < authored.Count; i++)
            {
                var spec = authored[i].Authored;
                var kit = Climate.FarmlandKit(spec.kind);
                var grid = new MapFieldGrid(kit, spec.angle);
                var polygon = spec.points.Count > 0 ? MapGeometry.ToVectors(spec.points) : null;
                LayEnds(kit, grid, s => IsFree(polygon, s), i);
                ClaimFields(kit, grid, polygon, i, spec.angle);
            }
        }

        /// <summary>A sector point this farmland may still use.</summary>
        private bool IsFree(List<Vector2> polygon, Vector2 sectorPoint)
        {
            if (!Symmetry.InSector(sectorPoint))
            {
                return false;
            }

            if (polygon != null && !MapGeometry.Contains(polygon, sectorPoint))
            {
                return false;
            }

            return Land.TryCell(sectorPoint, out var x, out var z) && Land.IsFree(x, z);
        }

        /// <summary>Field-end tracks, linked into the road net, then the belts beside them.</summary>
        private void LayEnds(MapFarmlandKit kit, MapFieldGrid grid, Func<Vector2, bool> free, int index)
        {
            var belt = string.IsNullOrEmpty(kit.beltKit) ? null : Climate.BeltKit(kit.beltKit);
            var track = string.IsNullOrEmpty(kit.trackClass) ? null : Climate.RoadClass(kit.trackClass);
            var across = grid.Across(Land.Bounds);
            var (first, last) = grid.EndLines(Land.Bounds);
            var beltRuns = new List<(List<Vector2> Run, int Line)>();
            var trackRuns = new List<List<Vector2>>();
            for (var line = first; line <= last; line++)
            {
                var hasBelt = belt != null && Mod(line, kit.beltEvery) == 0;
                var u = grid.EndLine(line);
                if (hasBelt)
                {
                    beltRuns.AddRange(Runs(grid, u, across, free).Select(r => (r, line)));
                }

                if (track != null)
                {
                    var offset = hasBelt ? belt.width * 0.5f + track.width * 0.5f + 0.5f : 0f;
                    trackRuns.AddRange(Runs(grid, u + offset, across, free));
                }
            }

            LayTracks(track, kit.trackJoinDistance, trackRuns);
            var belts = new MapBeltPlanner(Context);
            beltRuns.ForEach(run => belts.PlanLine(belt, run.Run, index * 1000 + run.Line));
        }

        /// <summary>Links every run before laying any, so no run snaps onto another's fresh track.</summary>
        private void LayTracks(MapRoadClass track, float joinDistance, List<List<Vector2>> runs)
        {
            var links = new MapFieldTrackLinks(Context, joinDistance);
            var linked = new List<List<Vector2>>();
            foreach (var run in runs)
            {
                if (links.Link(run))
                {
                    linked.Add(run);
                }
            }

            var roads = new MapRoadPlanner(Context);
            linked.ForEach(run => roads.LayTrack(track, run));
        }

        /// <summary>
        /// Gives every free farmland cell to the field its sector point falls in: cells near a field's edge (or the
        /// symmetry seam) become its margin, cells near its ends its headland, and cells of slivers its leftover ground.
        /// </summary>
        private void ClaimFields(MapFarmlandKit kit, MapFieldGrid grid, List<Vector2> polygon, int index, float angle)
        {
            var fields = Assign(grid, polygon);
            var edge = MapLandDistance.ToRegionEdge(Land.Width, Land.Height, (x, z) => fields[z * Land.Width + x]);
            var (sizes, reach) = MapFieldFragments.Measure(fields, edge, Land.Width, Land.Height);
            var regions = new MapFieldRegions(Context, kit, index, angle);
            var noise = Context.Noise(MapNoiseChannel.FieldMargins);
            var minCells = kit.minField / (MapLand.CellSize * MapLand.CellSize);
            Land.ForEach((x, z, p) =>
            {
                var i = z * Land.Width + x;
                var field = fields[i];
                if (field == NoField)
                {
                    return;
                }

                var sector = Symmetry.ToSector(p, out var copy);
                var width = kit.marginWidth * (1f + 0.45f * noise.Sample(p, 14f));
                var seam = Symmetry.SeamDistance(sector) < kit.marginWidth;
                if (edge[i] * MapLand.CellSize < width || seam)
                {
                    Land.Claim(x, z, regions.Margin);
                    return;
                }

                if (sizes[i] < minCells || reach[i] * MapLand.CellSize < kit.minFieldWidth * 0.5f)
                {
                    Land.Claim(x, z, regions.Leftover(field));
                    return;
                }

                var headland = kit.headlandWidth > 0f
                    && IsFieldEnd(fields, grid, sector, copy, field, kit.marginWidth + kit.headlandWidth);
                Land.Claim(x, z, headland ? regions.Headland(field) : regions.Field(field));
            });
        }

        /// <summary>Whether the field ends within <paramref name="reach"/> along the rows, in a stretch long enough to turn.</summary>
        private bool IsFieldEnd(int[] fields, MapFieldGrid grid, Vector2 sector, int copy, int field, float reach)
        {
            var transform = Symmetry.Copies[copy];
            var ahead = Run(fields, transform, sector, grid.Forward, field, HeadlandBody * reach);
            var behind = Run(fields, transform, sector, -grid.Forward, field, HeadlandBody * reach);
            return Mathf.Min(ahead, behind) < reach && ahead + behind >= HeadlandBody * reach;
        }

        /// <summary>Metres of field ahead along <paramref name="way"/>, up to <paramref name="limit"/>.</summary>
        private float Run(int[] fields, MapTransform transform, Vector2 sector, Vector2 way, int field, float limit)
        {
            var step = SampleStep;
            while (step <= limit && InField(fields, transform.Apply(sector + way * step), field))
            {
                step += SampleStep;
            }

            return step;
        }

        private bool InField(int[] fields, Vector2 point, int field) =>
            Land.TryCell(point, out var x, out var z) && fields[z * Land.Width + x] == field;

        /// <summary>The field key of every free farmland cell, <see cref="NoField"/> elsewhere.</summary>
        private int[] Assign(MapFieldGrid grid, List<Vector2> polygon)
        {
            var fields = new int[Land.Width * Land.Height];
            Land.ForEach((x, z, p) =>
            {
                var i = z * Land.Width + x;
                fields[i] = NoField;
                if (!Land.IsFree(x, z))
                {
                    return;
                }

                var sector = Symmetry.ToSector(p, out _);
                if (polygon == null || MapGeometry.Contains(polygon, sector))
                {
                    fields[i] = grid.Field(sector);
                }
            });
            return fields;
        }

        /// <summary>Stretches of the line u = const where the farmland is free, as sector polylines.</summary>
        private static IEnumerable<List<Vector2>> Runs(MapFieldGrid grid, float u, Vector2 across, Func<Vector2, bool> free)
        {
            var run = new List<Vector2>();
            for (var v = across.x; v <= across.y; v += SampleStep)
            {
                var point = grid.FromGrid(u, v);
                if (free(point))
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
