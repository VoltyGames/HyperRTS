using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>River channels and ponds, claimed first: nothing builds in water.</summary>
    public sealed class MapWaterPlanner : MapLandPass
    {
        public MapWaterPlanner(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            var shapes = Context.Shapes;
            if (shapes.Rivers.Count == 0 && shapes.Ponds.Count == 0)
            {
                return;
            }

            var bed = Context.Region(MapLandKind.Water, Climate.shoreSurface);
            foreach (var river in shapes.Rivers)
            {
                Land.ClaimWhere(river.Line.Bounds, bed, p => river.EdgeDistance(p) < 0f);
            }

            foreach (var pond in shapes.Ponds)
            {
                Land.ClaimWhere(pond.Bounds, bed, p => pond.EdgeDistance(p) < 0f);
            }
        }
    }
}
