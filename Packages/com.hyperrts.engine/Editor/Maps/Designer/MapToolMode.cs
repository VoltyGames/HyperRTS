namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor's canvas tools, in toolbar order.</summary>
    public enum MapToolMode
    {
        /// <summary>V: select, move, reshape and delete features.</summary>
        Select,

        /// <summary>P: add a point or circle feature per click.</summary>
        Point,

        /// <summary>L: draw a line feature (river, road, ravine, belt, line).</summary>
        Line,

        /// <summary>A: draw an area feature (wood, farmland, area, scatter).</summary>
        Area,
    }
}
