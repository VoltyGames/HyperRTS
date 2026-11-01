using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A spec feature along a polyline or around a polygon, which the symmetry moves point by point.</summary>
    public interface IMapLine
    {
        List<MapPoint> Points { get; }
    }
}
