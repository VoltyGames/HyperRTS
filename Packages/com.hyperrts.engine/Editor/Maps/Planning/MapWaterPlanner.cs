using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>River channels, claimed first: nothing builds in water.</summary>
    public sealed class MapWaterPlanner : MapLandStep
    {
        public MapWaterPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            var rivers = Context.Shapes.Rivers;
            if (rivers.Count == 0)
            {
                return;
            }

            var bed = Context.Region(MapLandKind.Water, Climate.shoreSurface);
            foreach (var river in rivers)
            {
                Land.ClaimWhere(river.Line.Bounds, bed, p => river.EdgeDistance(p) < 0f);
            }
        }
    }
}
