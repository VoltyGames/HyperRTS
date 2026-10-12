using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Spec;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// Settlements: up to <c>count</c> plots lining both sides of the roads within the radius, nearest the centre
    /// first, each with its yard, garden and back claimed on the kit's surfaces, a house facing the street,
    /// outbuildings, orchard, extras, fences and gate; poles and pipe run along the streets. Everything is chosen in
    /// the authored sector and placed and claimed in every copy. Runs after roads, banks, bases, pads and ravines and
    /// before woods, belts and farmland, so it takes only land they left free.
    /// </summary>
    public static class MapSettlementPlanner
    {
        private const int Channel = 32;

        public static void Plan(MapPlanContext context)
        {
            foreach (var settlement in context.Layout.Settlements)
            {
                if (settlement.Copy == 0)
                {
                    Plan(context, settlement.Authored, settlement.Index);
                }
            }
        }

        private static void Plan(MapPlanContext context, MapSettlementSpec spec, int index)
        {
            var label = $"Settlement {index + 1} ('{spec.kind}' at {spec.center})";
            var kit = context.Palette.SettlementKit(spec.kind);
            if (kit == null)
            {
                context.Issues.Error($"{label}: settlement kit '{spec.kind}' is not in the palette.");
                return;
            }

            var entries = MapSettlementEntries.Resolve(context, kit);
            if (entries == null || spec.count <= 0)
            {
                return;
            }

            var center = spec.center.ToVector2();
            var streets = MapStreets.Near(context, center, spec.radius);
            if (streets.Count == 0)
            {
                context.Issues.Warn($"{label} fit 0 of {spec.count} plots: no road within {spec.radius:0.#} m.");
                return;
            }

            MapStreetFurniture.Place(context, kit, entries, streets, center, spec.radius);
            var rejections = new MapPlotRejections();
            var plots = MapPlotFinder.Find(context, kit, streets, center, spec.radius, index, rejections);
            var built = Build(context, kit, entries, plots, spec.count, index, rejections);
            if (built < spec.count)
            {
                var why = rejections.Total > 0 ? $"rejected {rejections}" : "no plot fits along the roads";
                context.Issues.Warn($"{label} fit {built} of {spec.count} plots: {why}.");
            }
        }

        private static int Build(MapPlanContext context, MapSettlementKit kit, MapSettlementEntries entries,
            List<MapPlot> plots, int count, int index, MapPlotRejections rejections)
        {
            var builder = new MapPlotBuilder(context, kit, entries, MapPlacer.Random(context.Seed, Channel, index));
            var built = 0;
            foreach (var plot in plots)
            {
                if (built >= count)
                {
                    break;
                }

                var seed = unchecked(context.Seed * 31 + index * 7919 + built * 104729);
                var problem = TryBuild(context, builder, kit, plot, seed);
                if (problem == null)
                {
                    built++;
                }
                else
                {
                    rejections.Add(problem, plot.Center);
                }
            }

            return built;
        }

        /// <summary>Builds the plot if its land is free and its house fits; otherwise returns why not.</summary>
        private static string TryBuild(MapPlanContext context, MapPlotBuilder builder, MapSettlementKit kit,
            MapPlot plot, int seed)
        {
            var blocked = MapPlotClaims.Blocked(context, plot);
            if (blocked != null)
            {
                return blocked;
            }

            var house = builder.Choose(plot, out var problem);
            if (house == null)
            {
                return problem;
            }

            MapPlotClaims.Claim(context, plot, kit, seed);
            builder.Build(plot, house);
            return null;
        }
    }
}
