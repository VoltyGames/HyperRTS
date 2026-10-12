using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>Spec areas: hand-drawn polygons of one surface, for the odd patch the planners don't make.</summary>
    public static class MapAreaPlanner
    {
        public static void Plan(MapPlanContext context)
        {
            foreach (var area in context.Layout.Areas)
            {
                var polygon = MapGeometry.ToVectors(area.Placed.points);
                var region = context.Region(MapLandKind.Area, area.Placed.kind, area.Index);
                context.Land.ClaimWhere(MapGeometry.Bounds(polygon), region, p => MapGeometry.Contains(polygon, p));
            }
        }
    }
}
