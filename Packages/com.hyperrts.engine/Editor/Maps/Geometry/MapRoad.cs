using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Spec;

namespace HyperRTS.Editor.Maps.Geometry;

/// <summary>A road in map space: its centre line and class, authored in the spec or laid by a planner (tracks).</summary>
/// <param name="specIndex">The authored road's index in the spec's roads; -1 for a generated one.</param>
public sealed class MapRoad(MapLine line, MapRoadClass roadClass, bool generated, int specIndex = -1)
{
    public MapLine Line { get; } = line;

    public MapRoadClass Class { get; } = roadClass;

    /// <summary>Laid by a planner (field and access tracks) rather than authored; never levelled.</summary>
    public bool Generated { get; } = generated;

    /// <summary>The authored road issues about this one point to; none for a generated road.</summary>
    public MapIssueFeature Feature { get; } = specIndex < 0 ? MapIssueFeature.None : new MapIssueFeature(nameof(MapSpec.roads), specIndex);

    public float Width => Line.Width;
}
