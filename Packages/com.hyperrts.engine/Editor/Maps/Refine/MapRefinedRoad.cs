using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Refine;

/// <summary>An authored road while <see cref="MapRoadRefiner"/> works on it: its class and its rounded centre line.</summary>
public sealed class MapRefinedRoad(MapRoadSpec spec, MapRoadClass roadClass, List<Vector2> points)
{
    public MapRoadSpec Spec { get; } = spec;

    public MapRoadClass Class { get; } = roadClass;

    public List<Vector2> Points { get; set; } = points;
}
