using HyperRTS.Editor.Maps.Data;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>A road in map space: its centre line and class, authored in the spec or laid by a planner (tracks).</summary>
    public sealed class MapRoad
    {
        public MapRoad(MapLine line, MapRoadClass roadClass, bool generated)
        {
            Line = line;
            Class = roadClass;
            Generated = generated;
        }

        public MapLine Line { get; }

        public MapRoadClass Class { get; }

        /// <summary>Laid by a planner (field and access tracks) rather than authored; never levelled.</summary>
        public bool Generated { get; }

        public float Width => Line.Width;
    }
}
