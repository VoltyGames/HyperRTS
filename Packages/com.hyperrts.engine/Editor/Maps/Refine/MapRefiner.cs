using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;

namespace HyperRTS.Editor.Maps.Refine
{
    /// <summary>
    /// Turns the authored sector's coarse lines into natural shapes before the symmetry copies them: meandering
    /// rivers, curving roads that join each other, forking ravines. Works on the loaded spec in place; the file on
    /// disk keeps the coarse lines.
    /// </summary>
    public static class MapRefiner
    {
        public static void Refine(MapSpec spec, MapClimate climate)
        {
            var symmetry = MapSymmetry.Parse(spec.symmetry);
            for (var i = 0; i < spec.rivers.Count; i++)
            {
                MapRiverRefiner.Refine(spec.rivers[i], spec.seed, i);
            }

            MapRavineRefiner.Refine(spec, symmetry);
            MapRoadRefiner.Refine(spec, climate, symmetry);
        }
    }
}
