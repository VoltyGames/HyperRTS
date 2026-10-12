using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>Claims the land of every road: the carriageway, then the grass and weed verge either side.</summary>
    public static class MapRoadPlanner
    {
        public static void Plan(MapPlanContext context)
        {
            foreach (var road in context.Shapes.Roads)
            {
                Claim(context, road);
            }
        }

        /// <summary>Claims one road's land; planners call it for the tracks they lay.</summary>
        public static void Claim(MapPlanContext context, MapRoad road)
        {
            var roadClass = road.Class;
            var half = road.Width * 0.5f;
            var surface = context.Region(MapLandKind.Road, roadClass.surface);
            if (roadClass.vergeWidth <= 0f)
            {
                context.Land.ClaimWhere(road.Line.Bounds, surface, p => road.Line.Distance(p) < half);
                return;
            }

            var verge = context.Region(MapLandKind.Verge, roadClass.vergeSurface);
            var reach = half + roadClass.vergeWidth;
            context.Land.ClaimEach(road.Line.Bounds, p =>
            {
                var distance = road.Line.Distance(p);
                if (distance < half)
                {
                    return surface;
                }

                return distance < reach ? verge : MapLand.Unclaimed;
            });
        }
    }
}
