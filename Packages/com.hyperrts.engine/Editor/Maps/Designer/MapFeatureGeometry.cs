namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>How the Map Editor draws, hit-tests and drags one kind of spec feature.</summary>
    public enum MapFeatureGeometry
    {
        /// <summary>One position (spawn, resource, prop).</summary>
        Point,

        /// <summary>An open run of points (river, road, belt).</summary>
        Polyline,

        /// <summary>A closed ring of points (wood, area, scatter).</summary>
        Polygon,

        /// <summary>A position with a radius (hill, settlement).</summary>
        Circle,
    }
}
