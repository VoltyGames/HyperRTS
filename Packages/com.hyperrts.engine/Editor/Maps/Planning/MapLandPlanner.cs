using HyperRTS.Editor.Maps.Planning.Farmland;
using HyperRTS.Editor.Maps.Planning.Settlements;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Lays out the land in priority order, each pass claiming only what earlier ones left: water, roads, banks, bases
    /// and pads, access tracks, ravines, settlements (and spec tree rows), spec areas, woods, belts, farmland, then
    /// meadow everywhere else. Finally the surfaces grow their own trees and props.
    /// </summary>
    public static class MapLandPlanner
    {
        public static void Plan(MapPlanContext context)
        {
            MapLandPass[] passes =
            {
                new MapWaterPlanner(context),
                new MapRoadPlanner(context),
                new MapBankPlanner(context),
                new MapBasePlanner(context),
                new MapAccessPlanner(context),
                new MapRavinePlanner(context),
                new MapSettlementPlanner(context),
                new MapRowPlanter(context),
                new MapAreaPlanner(context),
                new MapWoodPlanner(context),
                new MapBeltPlanner(context),
                new MapFarmlandPlanner(context),
                new MapMeadowPlanner(context),
                new MapSurfacePlanter(context),
            };
            foreach (var pass in passes)
            {
                pass.Plan();
            }
        }
    }
}
