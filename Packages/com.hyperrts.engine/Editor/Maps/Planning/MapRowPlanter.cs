using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Spec lines of a climate tree (a row of poplars along a road, an avenue of oaks): one tree every spacing
    /// metres with a little jitter. Prop lines (poles, fences) are the placer's.
    /// </summary>
    public static class MapRowPlanter
    {
        public static void Plan(MapPlanContext context)
        {
            foreach (var line in context.Layout.Lines.Where(l => l.Copy == 0))
            {
                var tree = context.Climate.TreeIndex(line.Authored.kind);
                if (tree < 0)
                {
                    continue;
                }

                var points = MapGeometry.ToVectors(line.Authored.points);
                var length = MapGeometry.Length(points);
                var path = new MapPathWalker(points);
                var step = Mathf.Max(2f, line.Authored.spacing);
                var random = Placement.MapPlacer.Random(context.Seed, 11, line.Index);
                for (var along = step * 0.5f; along < length; along += step)
                {
                    var p = path.PointAt(along + Jitter(random, step * 0.2f), out var direction);
                    p += new Vector2(direction.y, -direction.x) * Jitter(random, 0.6f);
                    if (context.Layout.Symmetry.InSector(p) && context.Spacing.Fits(p, step * 0.5f))
                    {
                        context.PlantCopies(tree, p, context.TreeScale(tree, random), (float)random.NextDouble() * 360f);
                    }
                }
            }
        }

        private static float Jitter(System.Random random, float amplitude) => ((float)random.NextDouble() * 2f - 1f) * amplitude;
    }
}
