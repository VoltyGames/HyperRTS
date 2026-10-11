using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
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
        private const float RavineWobble = 1.5f;
        private const float RavineFloor = 0.3f;
        private const float FordEdge = 3f;
        private const float RampLength = 14f;
        private const float RampEdge = 3f;
        private const float AbutmentDrop = 0.4f;

        public static MapHeights Shape(MapLayout layout, MapShapes shapes)
        {
            var terrain = layout.Terrain;
            var heights = new MapHeights(terrain.resolution, layout.TerrainBounds);
            var relief = new MapNoise(layout.Symmetry, layout.Spec.seed, 0);
            var wobble = new MapNoise(layout.Symmetry, layout.Spec.seed, 1);
            for (var z = 0; z < heights.Resolution; z++)
            {
                for (var x = 0; x < heights.Resolution; x++)
                {
                    var p = heights.Position(x, z);
                    var h = terrain.height + terrain.relief * relief.Sample(p, terrain.reliefScale) + Hills(layout, p);
                    h = Rivers(shapes, p, h, terrain.waterLevel, wobble.Sample(p, 25f) * BankWobble);
                    heights[x, z] = Fords(shapes, p, h, terrain.waterLevel);
                }
            }

            var open = heights.Copy();
            CutRavines(heights, shapes, wobble, terrain.waterLevel);
            MapRoadLeveller.Level(heights, open, shapes, terrain.waterLevel);
            RaiseBridges(heights, shapes);
            return heights;
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

        /// <summary>A parabolic bed below the water, a low bank just above it, then the valley side easing up.</summary>
        private static float Rivers(MapShapes shapes, Vector2 p, float h, float water, float wobble)
        {
            foreach (var (line, spec) in shapes.Rivers)
            {
                var distance = line.Distance(p) + wobble;
                var half = spec.width * 0.5f;
                var valley = spec.valleyWidth * 0.5f;
                if (distance >= valley)
                {
                    continue;
                }

                float target;
                if (distance < half)
                {
                    var t = distance / half;
                    target = water - spec.depth * (1f - t * t);
                }
                else
                {
                    var bank = Mathf.Lerp(water, water + BankLift, (distance - half) / BankDrop);
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

        /// <summary>Flat floor, steep walls; ravines open out gently at their ends and never cut down to the water.</summary>
        private static void CutRavines(MapHeights heights, MapShapes shapes, MapNoise wobble, float water)
        {
            foreach (var (line, spec) in shapes.Ravines)
            {
                heights.ForEachIn(line.Bounds, (x, z, p) =>
                {
                    var distance = line.Distance(p, out var along) + wobble.Sample(p, 15f) * RavineWobble;
                    var half = spec.width * 0.5f;
                    if (distance >= half)
                    {
                        return;
                    }

                    var u = distance / half;
                    var wall = u < RavineFloor ? 1f : 1f - MapGeometry.Smooth((u - RavineFloor) / (1f - RavineFloor));
                    var taper = MapGeometry.Smooth(Mathf.Min(along, line.Length - along) / spec.width);
                    var h = heights[x, z];
                    heights[x, z] = Mathf.Max(h - spec.depth * wall * taper, Mathf.Min(h, water + 0.5f));
                });
            }
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
