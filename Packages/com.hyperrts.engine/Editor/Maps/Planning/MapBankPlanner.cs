using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// River banks and the floodplain meadows along them inside the valley, claimed once roads have taken their
    /// crossings.
    /// </summary>
    public sealed class MapBankPlanner : MapLandPass
    {
        public MapBankPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            var rivers = Context.Shapes.Rivers;
            if (rivers.Count == 0)
            {
                return;
            }

            var bank = Context.Region(MapLandKind.Bank, Climate.shoreSurface);
            var floodplain = Context.Region(MapLandKind.Floodplain, Climate.floodplainSurface);
            var noise = Context.Noise(MapNoiseChannel.Banks);
            foreach (var river in rivers)
            {
                var valley = river.Spec.valleyWidth * 0.5f;
                Land.ClaimEach(river.Line.Bounds, p =>
                {
                    var edgeDistance = river.EdgeDistance(p);
                    var edge = edgeDistance + noise.Sample(p, 9f) * 1.5f;
                    if (edge < Climate.shoreWidth)
                    {
                        return bank;
                    }

                    var reach = Climate.shoreWidth + Climate.floodplainWidth * (1f + 0.4f * noise.Sample(p, 30f));
                    var onFloodplain = edgeDistance < reach && river.Line.Distance(p) < valley;
                    return onFloodplain ? floodplain : MapLand.Unclaimed;
                });
            }
        }
    }
}
