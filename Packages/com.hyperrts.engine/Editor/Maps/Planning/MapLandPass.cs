using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Symmetry;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// One step of land planning: it claims the land it is for, taking only what earlier passes left, and may place
    /// props and plant trees. Choices are made in the authored sector and copied by the symmetry. Constructors only
    /// keep the context; the work happens in <see cref="Plan"/>, in <see cref="MapLandPlanner"/>'s order.
    /// </summary>
    public abstract class MapLandPass
    {
        protected MapLandPass(MapPlanContext context) => Context = context;

        protected MapPlanContext Context { get; }

        protected MapLand Land => Context.Land;

        protected MapClimate Climate => Context.Climate;

        protected MapSymmetry Symmetry => Context.Layout.Symmetry;

        public abstract void Plan();
    }
}
