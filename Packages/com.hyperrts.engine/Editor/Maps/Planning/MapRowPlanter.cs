using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Spec lines of a climate tree (a row of poplars along a road, an avenue of oaks): one tree every spacing
    /// metres with a little jitter. Prop lines (poles, fences) are the prop placer's.
    /// </summary>
    public sealed class MapRowPlanter : MapLandPass
    {
        public MapRowPlanter(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            foreach (var line in Context.Layout.Lines.Where(l => l.Copy == 0 && Climate.HasTree(l.Authored.kind)))
            {
                var tree = Climate.TreeIndex(line.Authored.kind);
                var points = MapGeometry.ToVectors(line.Authored.points);
                var length = MapGeometry.Length(points);
                var path = new MapPathWalker(points);
                var step = Mathf.Max(2f, line.Authored.spacing);
                var random = Context.Random(MapRandomChannel.TreeRows, line.Index);
                for (var along = step * 0.5f; along < length; along += step)
                {
                    var p = path.PointAt(along + random.Signed() * (step * 0.2f), out var direction);
                    p += MapGeometry.Right(direction) * (random.Signed() * 0.6f);
                    if (Symmetry.InSector(p) && Context.Trees.Fits(p, step * 0.5f))
                    {
                        Context.Trees.Plant(tree, p, random);
                    }
                }
            }
        }
    }
}
