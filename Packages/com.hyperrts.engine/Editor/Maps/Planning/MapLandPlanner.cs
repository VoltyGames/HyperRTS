using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Lays out the land in priority order, each planner claiming only what earlier ones left: water, roads, banks,
    /// bases and pads, access tracks, ravines, settlements (and spec tree rows), spec areas, woods, belts, farmland,
    /// then meadow everywhere else. Finally the surfaces grow their own trees and props.
    /// </summary>
    public static class MapLandPlanner
    {
        public static void Plan(MapPlanContext context)
        {
            MapWaterPlanner.ClaimWater(context);
            MapRoadPlanner.Plan(context);
            MapWaterPlanner.ClaimBanks(context);
            MapBasePlanner.Plan(context);
            MapAccessPlanner.Plan(context);
            MapRavinePlanner.Plan(context);
            MapSettlementPlanner.Plan(context);
            MapRowPlanter.Plan(context);
            MapAreaPlanner.Plan(context);
            MapWoodPlanner.Plan(context);
            MapBeltPlanner.PlanSpecBelts(context);
            MapFarmlandPlanner.Plan(context);
            FillMeadow(context);
            MapSurfacePlanter.Plant(context);
        }

        private static void FillMeadow(MapPlanContext context)
        {
            var meadow = context.Region(MapLandKind.Meadow, context.Climate.groundSurface, "open ground");
            context.Land.ClaimWhere(context.Land.Bounds, meadow, _ => true);
        }
    }
}
