using HyperRTS.Presentation.Interaction;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Overlays;

/// <summary>A formation's extent across its facing and its leading edge, measured from the drag's press point.</summary>
public struct FrontEdge(in FacingDragState drag)
{
    private readonly float3 _origin = drag.Press;
    private readonly float2 _forward = drag.Facing;
    private readonly float2 _across = new(drag.Facing.y, -drag.Facing.x);

    public float Left = float.MaxValue;
    public float Right = float.MinValue;
    public float Lead = float.MinValue;

    /// <summary>Widens the edge to take a slot whose footprint has <paramref name="halfSize"/> across and along.</summary>
    public void Include(float3 slot, float2 halfSize)
    {
        var offset = slot.xz - _origin.xz;
        var across = math.dot(offset, _across);
        Left = math.min(Left, across - halfSize.x);
        Right = math.max(Right, across + halfSize.x);
        Lead = math.max(Lead, math.dot(offset, _forward) + halfSize.y);
    }

    /// <summary>The ground point <paramref name="across"/> to the side and <paramref name="ahead"/> past the lead.</summary>
    public readonly float3 Point(float across, float ahead)
    {
        var xz = _origin.xz + _across * across + _forward * (Lead + ahead);
        return new float3(xz.x, _origin.y, xz.y);
    }
}
