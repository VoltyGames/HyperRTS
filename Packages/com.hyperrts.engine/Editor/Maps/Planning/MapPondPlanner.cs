using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// The land round each pond: the dam's body and track, then the wet margin whose surface grows willows. Fails a
    /// pond that reaches a river, a road or a base.
    /// </summary>
    public sealed class MapPondPlanner : MapLandPass
    {
        private const float CheckStep = 3f;
        private const float Clearance = 2f;

        public MapPondPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            var noise = Context.Noise(MapNoiseChannel.Banks);
            var ponds = Context.Shapes.Ponds;
            for (var i = 0; i < ponds.Count; i++)
            {
                var pond = ponds[i];
                var feature = new MapIssueFeature(nameof(MapSpec.ponds), Context.Layout.Ponds[i].Index);
                CheckClear(pond, feature);
                var shore = Context.Region(MapLandKind.Bank, pond.Spec.shore);
                var dam = pond.HasDam ? Context.Region(MapLandKind.Bank, pond.Spec.damSurface) : MapLand.Unclaimed;
                Land.ClaimEach(pond.Bounds, p =>
                {
                    if (pond.OnDam(p))
                    {
                        return dam;
                    }

                    var edge = pond.EdgeDistance(p) + noise.Sample(p, 9f) * 1.5f;
                    return edge < pond.Spec.shoreWidth ? shore : MapLand.Unclaimed;
                });
            }
        }

        /// <summary>A pond's water and dam stay clear of rivers, roads and bases.</summary>
        private void CheckClear(MapPond pond, MapIssueFeature feature)
        {
            var shapes = Context.Shapes;
            var baseRadius = Context.Palette.baseRadius;
            var number = feature.index + 1;
            for (var x = pond.Bounds.xMin; x <= pond.Bounds.xMax; x += CheckStep)
            {
                for (var z = pond.Bounds.yMin; z <= pond.Bounds.yMax; z += CheckStep)
                {
                    var p = new Vector2(x, z);
                    if (pond.EdgeDistance(p) > Clearance && !pond.OnDam(p))
                    {
                        continue;
                    }

                    var crossing = FindCrossing(shapes, p, baseRadius);
                    if (crossing != null)
                    {
                        Context.Issues.Error($"Pond {number} crosses {crossing} near ({p.x:0}, {p.y:0}).", feature);
                        return;
                    }
                }
            }
        }

        private string FindCrossing(MapShapes shapes, Vector2 p, float baseRadius)
        {
            foreach (var river in shapes.Rivers)
            {
                if (river.EdgeDistance(p) < Clearance)
                {
                    return "a river";
                }
            }

            if (shapes.RoadEdgeDistance(p) < Clearance)
            {
                return "a road";
            }

            foreach (var spawn in Context.Layout.Spawns)
            {
                if ((spawn.Position - p).magnitude < baseRadius)
                {
                    return $"the base of slot {spawn.Slot}";
                }
            }

            return null;
        }
    }
}
