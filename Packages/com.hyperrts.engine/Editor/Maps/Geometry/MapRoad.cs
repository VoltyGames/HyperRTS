using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Spec;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>A road in map space: its centre line and class, authored in the spec or laid by a planner (tracks).</summary>
    public sealed class MapRoad
    {
        /// <param name="specIndex">The authored road's index in the spec's roads; -1 for a generated one.</param>
        public MapRoad(MapLine line, MapRoadClass roadClass, bool generated, int specIndex = -1)
        {
            Line = line;
            Class = roadClass;
            Generated = generated;
            Feature = specIndex < 0 ? MapIssueFeature.None : new MapIssueFeature(nameof(MapSpec.roads), specIndex);
        }

        public MapLine Line { get; }

        public MapRoadClass Class { get; }

        /// <summary>Laid by a planner (field and access tracks) rather than authored; never levelled.</summary>
        public bool Generated { get; }

        /// <summary>The authored road issues about this one point to; none for a generated road.</summary>
        public MapIssueFeature Feature { get; }

        public float Width => Line.Width;
    }
}
