using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>Open meadow on every cell the other passes left.</summary>
    public sealed class MapMeadowPlanner : MapLandStep
    {
        public MapMeadowPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            var meadow = Context.Region(MapLandKind.Meadow, Climate.groundSurface);
            Land.ClaimWhere(Land.Bounds, meadow, _ => true);
        }
    }
}
