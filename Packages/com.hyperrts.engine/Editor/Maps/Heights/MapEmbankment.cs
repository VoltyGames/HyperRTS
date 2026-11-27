using HyperRTS.Editor.Maps.Geometry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Heights;

/// <summary>
/// The ground a bridge stands on: an embankment behind each abutment, level under the approach slabs and ramped
/// down to the ground, with the river's own banks left between the abutments.
/// </summary>
public sealed class MapEmbankment
{
    private const float RampEdge = 3f;
    private const float CutEdge = 8f;
    private const float MaxCutEdge = 24f;
    private const float LandingDrop = 0.05f;
    private const float AbutmentDrop = 0.4f;

    private readonly MapCrossing _crossing;
    private readonly Ramp _ahead;
    private readonly Ramp _behind;

    public MapEmbankment(MapHeights heights, MapCrossing crossing)
    {
        _crossing = crossing;
        _ahead = Ramp.Measure(heights, crossing, 1f);
        _behind = Ramp.Measure(heights, crossing, -1f);
    }

    /// <summary>
    /// Raises both embankments into <paramref name="heights"/>. Under the span the ground goes back to
    /// <paramref name="carved"/>, as it lay before roads were levelled across the river.
    /// </summary>
    public void Raise(MapHeights heights, MapHeights carved)
    {
        var crossing = _crossing;
        var level = crossing.Height - LandingDrop;
        var reach = _crossing.Reach + Mathf.Max(_ahead.Length, _behind.Length) + crossing.Width + MaxCutEdge;
        heights.ForEachNear(crossing.Position, reach, (x, z, _) =>
        {
            var p = heights.Position(x, z);
            var local = crossing.Local(p);
            var ramp = local.y < 0f ? _behind : _ahead;
            var along = Mathf.Abs(local.y);
            if (along > _crossing.Reach + ramp.Length)
            {
                return;
            }

            var h = Mathf.Lerp(heights[x, z], carved[x, z], crossing.SpanOpening(p));
            var underDeck = along <= crossing.Span * 0.5f;
            var target = underDeck ? crossing.Height - AbutmentDrop : ramp.Height(level, h, along - _crossing.Reach);
            heights[x, z] = Mathf.Lerp(h, target, Fill(local, along, Mathf.Abs(h - target)));
        });
    }

    /// <summary>
    /// Share of the embankment's height at a point: whole over the roadway behind the abutment, falling away down
    /// its sides and round the abutment's ends.
    /// </summary>
    private float Fill(Vector2 local, float along, float rise)
    {
        // A high bank eases out wider, so the sides stay drivable and gentle enough for the ground texture.
        var edge = Mathf.Clamp(rise * CutEdge, RampEdge, MaxCutEdge);
        var side = Mathf.Max(0f, Mathf.Abs(local.x) - (_crossing.Width * 0.5f + 1f));
        // The end slope's toe stays on the abutment face, so the banks in front keep their shape; its crest
        // recedes with distance from the roadway, wrapping a cone round the abutment's end.
        var front = Mathf.Max(0f, _crossing.Span * 0.5f + side - along);
        var reach = new Vector2(side / edge, front / (_crossing.AbutmentDepth + side)).magnitude;
        return 1f - MapGeometry.Smooth(reach);
    }

    /// <summary>
    /// The ground's climb from the level landing past one end of a deck: as long as it takes to meet the ground
    /// at an easy grade.
    /// </summary>
    private readonly struct Ramp
    {
        private const float MinLength = 14f;
        private const float MaxLength = 80f;
        private const float Step = 2f;
        private const float Grade = 0.12f;

        public readonly float Length;
        private readonly float _top;

        private Ramp(float length, float top)
        {
            Length = length;
            _top = top;
        }

        public static Ramp Measure(MapHeights heights, MapCrossing crossing, float sign)
        {
            var end = crossing.Position + crossing.Across * (sign * crossing.Reach);
            var length = MinLength;
            var top = heights.Height(end + crossing.Across * (sign * length));
            while (length < MaxLength && Mathf.Abs(top - crossing.Height) > Grade * length)
            {
                length += Step;
                top = heights.Height(end + crossing.Across * (sign * length));
            }

            return new Ramp(length, top);
        }

        /// <summary>The height <paramref name="along"/> metres up it, meeting the ground <paramref name="h"/> at its top.</summary>
        public float Height(float deck, float h, float along)
        {
            var t = Mathf.Clamp01(along / Length);
            return Mathf.Lerp(deck, Mathf.Lerp(_top, h, t), MapGeometry.Smooth(t));
        }
    }
}
