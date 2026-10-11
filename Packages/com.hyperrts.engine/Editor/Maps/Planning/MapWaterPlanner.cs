using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Rivers: the channel first (nothing builds in water), then, once roads have taken their crossings, the banks
    /// and the floodplain meadows along them inside the valley.
    /// </summary>
    public static class MapWaterPlanner
    {
        public static void ClaimWater(MapPlanContext context)
        {
            if (context.Shapes.Rivers.Count == 0)
            {
                return;
            }

            var bed = context.Region(MapLandKind.Water, context.Climate.shoreSurface, "river bed");
            foreach (var river in context.Shapes.Rivers)
            {
                context.Land.ClaimWhere(river.Line.Bounds, bed, p => river.EdgeDistance(p) < 0f);
            }
        }

        public static void ClaimBanks(MapPlanContext context)
        {
            if (context.Shapes.Rivers.Count == 0)
            {
                return;
            }

            var climate = context.Climate;
            var bank = context.Region(MapLandKind.Bank, climate.shoreSurface, "river bank");
            var floodplain = context.Region(MapLandKind.Floodplain, climate.floodplainSurface, "floodplain");
            var noise = new MapNoise(context.Layout.Symmetry, context.Seed, 21);
            foreach (var river in context.Shapes.Rivers)
            {
                var valley = river.Spec.valleyWidth * 0.5f;
                context.Land.ClaimWhere(river.Line.Bounds, bank, p =>
                {
                    var edge = river.EdgeDistance(p) + noise.Sample(p, 9f) * 1.5f;
                    return edge < climate.shoreWidth;
                });
                context.Land.ClaimWhere(river.Line.Bounds, floodplain, p =>
                {
                    var reach = climate.shoreWidth + climate.floodplainWidth * (1f + 0.4f * noise.Sample(p, 30f));
                    return river.EdgeDistance(p) < reach && river.Line.Distance(p) < valley;
                });
            }
        }
    }
}
