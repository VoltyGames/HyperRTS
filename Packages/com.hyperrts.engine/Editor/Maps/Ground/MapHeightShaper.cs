using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Shapes the ground from the layout: rolling relief and hills, river valleys and fords, ravines, then roads
    /// levelled (on embankments over ravines) and ramps up to bridge decks.
    /// </summary>
    public static class MapHeightShaper
    {
        private const float BankLift = 0.6f;
        private const float BankDrop = 4f;
        private const float BankWobble = 1.2f;
        private const float RavineFloor = 0.45f;
        private const float RimDrop = 0.12f;
        private const float SwellScale = 0.3f;
        private const float FordEdge = 3f;
        private const float RampLength = 14f;
        private const float RampEdge = 3f;
        private const float AbutmentDrop = 0.4f;

        public static MapHeights Shape(MapLayout layout, MapShapes shapes)
        {
            var terrain = layout.Terrain;
            var heights = new MapHeights(terrain.resolution, layout.TerrainBounds);
            var relief = new MapNoise(layout.Symmetry, layout.Spec.seed, 0);
            var wobble = new MapNoise(layout.Symmetry, layout.Spec.seed, MapRavineWall.WobbleChannel);
            for (var z = 0; z < heights.Resolution; z++)
            {
                for (var x = 0; x < heights.Resolution; x++)
                {
                    var p = heights.Position(x, z);
                    var h = terrain.height + Relief(terrain, relief, p) + Hills(layout, p);
                    h = Rivers(shapes, p, h, terrain.waterLevel, wobble);
                    heights[x, z] = Fords(shapes, p, h, terrain.waterLevel);
                }
            }

            var open = heights.Copy();
            CutRavines(heights, shapes, wobble, terrain.waterLevel);
            MapRoadLeveller.Level(heights, open, shapes, terrain.waterLevel);
            RaiseBridges(heights, shapes);
            return heights;
        }

        /// <summary>Broad swells of the plateau with small undulations on them.</summary>
        private static float Relief(MapTerrainSpec terrain, MapNoise relief, Vector2 p)
        {
            var swells = relief.Sample(p, terrain.reliefScale);
            var undulation = relief.Sample(p + new Vector2(517f, 233f), terrain.reliefScale * SwellScale);
            return terrain.relief * (swells + undulation * 0.3f);
        }

        private static float Hills(MapLayout layout, Vector2 p)
        {
            var sum = 0f;
            foreach (var hill in layout.Hills)
            {
                var spec = hill.Placed;
                var distance = (p - spec.pos.ToVector2()).magnitude;
                if (distance >= spec.radius)
                {
                    continue;
                }

                var top = spec.flatTop * spec.radius;
                var t = distance <= top ? 0f : (distance - top) / (spec.radius - top);
                sum += spec.height * 0.5f * (1f + Mathf.Cos(Mathf.PI * t));
            }

            return sum;
        }

        /// <summary>
        /// A parabolic bed below the water, a low bank just above it, then the valley side easing up; the bank's
        /// steepness wanders so one reach has a cut bank and the next a gentle shelving shore.
        /// </summary>
        private static float Rivers(MapShapes shapes, Vector2 p, float h, float water, MapNoise wobble)
        {
            foreach (var river in shapes.Rivers)
            {
                var spec = river.Spec;
                var raw = river.Line.Distance(p);
                var valley = spec.valleyWidth * 0.5f;
                if (raw >= valley)
                {
                    continue;
                }

                var distance = raw + wobble.Sample(p, 25f) * BankWobble;
                var half = river.HalfWidth(p);
                var drop = BankDrop * (1f + 0.6f * wobble.Sample(p + new Vector2(91f, 47f), 60f));
                float target;
                if (distance < half)
                {
                    var t = distance / half;
                    target = water - spec.depth * (1f - t * t);
                }
                else
                {
                    var bank = Mathf.Lerp(water, water + BankLift, (distance - half) / Mathf.Max(1f, drop));
                    target = Mathf.Lerp(bank, h, MapGeometry.Smooth((distance - half) / (valley - half)));
                }

                h = Mathf.Min(h, target);
            }

            return h;
        }

        private static float Fords(MapShapes shapes, Vector2 p, float h, float water)
        {
            foreach (var crossing in shapes.Crossings)
            {
                if (crossing.IsBridge)
                {
                    continue;
                }

                var local = crossing.Local(p);
                var edge = Mathf.Abs(local.x) - crossing.Width * 0.5f;
                if (Mathf.Abs(local.y) > crossing.Span * 0.5f || edge > FordEdge)
                {
                    continue;
                }

                var bed = water - MapShapes.FordBed;
                if (h < bed)
                {
                    h = Mathf.Lerp(h, bed, 1f - MapGeometry.Smooth(edge / FordEdge));
                }
            }

            return h;
        }

        /// <summary>
        /// Flat floor, steep walls and a gentle rim slope easing into them; ravines open out gently at their ends and
        /// never cut down to the water. Where branches meet the main cut the deeper cut wins.
        /// </summary>
        private static void CutRavines(MapHeights heights, MapShapes shapes, MapNoise wobble, float water)
        {
            var open = heights.Copy();
            foreach (var (line, spec) in shapes.Ravines)
            {
                heights.ForEachIn(line.Bounds, (x, z, p) =>
                {
                    var distance = MapRavineWall.Distance(line, wobble, p, out var along);
                    var half = spec.width * 0.5f;
                    var outer = half + spec.rim;
                    if (distance >= outer)
                    {
                        return;
                    }

                    var taper = MapGeometry.Smooth(Mathf.Min(along, line.Length - along) / spec.width);
                    var cut = spec.depth * Profile(distance, half, outer) * taper;
                    var h = open[x, z];
                    var carved = Mathf.Max(h - cut, Mathf.Min(h, water + 0.5f));
                    heights[x, z] = Mathf.Min(heights[x, z], carved);
                });
            }
        }

        /// <summary>Share of the ravine's depth cut at a distance from its line: floor, wall, then the rim.</summary>
        private static float Profile(float distance, float half, float outer)
        {
            if (distance >= half)
            {
                return RimDrop * (1f - MapGeometry.Smooth((distance - half) / (outer - half)));
            }

            var u = distance / half;
            var wall = u < RavineFloor ? 1f : 1f - MapGeometry.Smooth((u - RavineFloor) / (1f - RavineFloor));
            return RimDrop + (1f - RimDrop) * wall;
        }

        /// <summary>Fills the banks under a deck to just below it and ramps the ground up to its ends.</summary>
        private static void RaiseBridges(MapHeights heights, MapShapes shapes)
        {
            foreach (var crossing in shapes.Crossings)
            {
                if (!crossing.IsBridge)
                {
                    continue;
                }

                var half = crossing.Span * 0.5f;
                heights.ForEachNear(crossing.Position, half + RampLength + crossing.Width, (x, z, _) =>
                {
                    var p = heights.Position(x, z);
                    var local = crossing.Local(p);
                    var side = Mathf.Abs(local.x) - (crossing.Width * 0.5f + 1f);
                    var along = Mathf.Abs(local.y);
                    if (side > RampEdge || along > half + RampLength)
                    {
                        return;
                    }

                    var h = heights[x, z];
                    float target;
                    if (along <= half)
                    {
                        if (shapes.RiverEdgeDistance(p) < 1f)
                        {
                            return;
                        }

                        target = crossing.Height - AbutmentDrop;
                    }
                    else
                    {
                        target = Mathf.Lerp(crossing.Height, h, MapGeometry.Smooth((along - half) / RampLength));
                    }

                    heights[x, z] = Mathf.Lerp(h, target, 1f - MapGeometry.Smooth(side / RampEdge));
                });
            }
        }
    }
}
