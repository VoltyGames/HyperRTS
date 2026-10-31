using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// Settlements: up to <c>count</c> plots lining both sides of the roads within the radius, nearest the centre
    /// first, each with its yard, garden and back claimed on the kit's surfaces, a house facing the street,
    /// outbuildings, orchard, extras, fences and gate; poles and pipe run along the streets. Everything is chosen in
    /// the authored sector and placed and claimed in every copy. Runs after roads, banks, bases, pads and ravines and
    /// before woods, belts and farmland, so it takes only land they left free.
    /// </summary>
    public sealed class MapSettlementPlanner : MapLandPass
    {
        public MapSettlementPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            foreach (var settlement in Context.Layout.Settlements)
            {
                if (settlement.Copy == 0 && settlement.Authored.count > 0)
                {
                    Plan(new MapSettlement(Context, settlement.Authored, settlement.Index));
                }
            }
        }

        private void Plan(MapSettlement settlement)
        {
            var spec = settlement.Spec;
            if (settlement.Streets.Count == 0)
            {
                Context.Issues.Warn($"{settlement.Label} fit 0 of {spec.count} plots: no road within {spec.radius:0.#} m.");
                return;
            }

            new MapStreetFurniture(settlement).Place();
            var plots = new MapPlotFinder(settlement).Find();
            var built = Build(settlement, plots);
            if (built < spec.count)
            {
                var rejections = settlement.Rejections;
                var why = rejections.Total > 0 ? $"rejected {rejections}" : "no plot fits along the roads";
                Context.Issues.Warn($"{settlement.Label} fit {built} of {spec.count} plots: {why}.");
            }
        }

        private int Build(MapSettlement settlement, List<MapPlot> plots)
        {
            var builder = new MapPlotBuilder(settlement, Context.Random(MapRandomChannel.Settlements, settlement.Index));
            var regionSeeds = Context.Random(MapRandomChannel.PlotRegions, settlement.Index);
            var built = 0;
            foreach (var plot in plots)
            {
                if (built >= settlement.Spec.count)
                {
                    break;
                }

                var seed = regionSeeds.Next();
                var problem = TryBuild(settlement, builder, plot, seed);
                if (problem == null)
                {
                    built++;
                }
                else
                {
                    settlement.Rejections.Add(problem, plot.Center);
                }
            }

            return built;
        }

        /// <summary>Builds the plot if its land is free and its house fits; otherwise returns why not.</summary>
        private static string TryBuild(MapSettlement settlement, MapPlotBuilder builder, MapPlot plot, int seed)
        {
            var blocked = settlement.Claims.Blocked(plot);
            if (blocked != null)
            {
                return blocked;
            }

            var house = builder.Choose(plot, out var problem);
            if (house == null)
            {
                return problem;
            }

            settlement.Claims.Claim(plot, seed);
            builder.Build(plot, house, seed);
            return null;
        }
    }
}
