using HyperRTS.Editor.Maps.Symmetry;

namespace HyperRTS.Editor.Maps.Layout
{
    /// <summary>One placed copy of an authored feature: the authored data, the copy in map space and how it got there.</summary>
    public sealed class MapCopy<T> where T : class
    {
        public MapCopy(T authored, T placed, MapTransform transform, int index, int copy)
        {
            Authored = authored;
            Placed = placed;
            Transform = transform;
            Index = index;
            Copy = copy;
        }

        public T Authored { get; }

        /// <summary>The feature in map space; equals <see cref="Authored"/>'s values for the identity copy.</summary>
        public T Placed { get; }

        public MapTransform Transform { get; }

        /// <summary>Position of the authored feature in its spec array; with the seed it keys random scattering.</summary>
        public int Index { get; }

        public int Copy { get; }
    }
}
