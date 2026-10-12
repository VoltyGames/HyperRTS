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

            var bed = context.Region(MapLandKind.Water, context.Climate.shoreSurface);
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
            var bank = context.Region(MapLandKind.Bank, climate.shoreSurface);
            var floodplain = context.Region(MapLandKind.Floodplain, climate.floodplainSurface);
            var noise = context.Noise(MapNoiseChannel.Banks);
            foreach (var river in context.Shapes.Rivers)
            {
                var valley = river.Spec.valleyWidth * 0.5f;
                context.Land.ClaimEach(river.Line.Bounds, p =>
                {
                    var edgeDistance = river.EdgeDistance(p);
                    var edge = edgeDistance + noise.Sample(p, 9f) * 1.5f;
                    if (edge < climate.shoreWidth)
                    {
                        return bank;
                    }

                    var reach = climate.shoreWidth + climate.floodplainWidth * (1f + 0.4f * noise.Sample(p, 30f));
                    var onFloodplain = edgeDistance < reach && river.Line.Distance(p) < valley;
                    return onFloodplain ? floodplain : MapLand.Unclaimed;
                });
            }
        }
    }
}
