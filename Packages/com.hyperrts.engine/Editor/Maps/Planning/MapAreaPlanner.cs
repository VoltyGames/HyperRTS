using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>Spec areas: hand-drawn polygons of one surface, for the odd patch the other passes don't make.</summary>
    public sealed class MapAreaPlanner : MapLandStep
    {
        public MapAreaPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            foreach (var area in Context.Layout.Areas)
            {
                var polygon = MapGeometry.ToVectors(area.Placed.points);
                var region = Context.Region(MapLandKind.Area, area.Placed.kind, area.Index);
                Land.ClaimWhere(MapGeometry.Bounds(polygon), region, p => MapGeometry.Contains(polygon, p));
            }
        }
    }
}
