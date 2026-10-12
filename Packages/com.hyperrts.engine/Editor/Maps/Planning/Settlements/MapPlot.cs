using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// One settlement plot in authored-sector space: a rectangle whose front edge lies beside the street, with
    /// <c>u</c> running along the street (centred on the plot) and <c>v</c> back from the front fence. The yard,
    /// garden and back are bands of <c>v</c>.
    /// </summary>
    internal sealed class MapPlot
    {
        public MapPlot(Vector2 front, Vector2 run, Vector2 back, float width, float depth)
        {
            Front = front;
            Run = run;
            Back = back;
            Width = width;
            Depth = depth;
        }

        /// <summary>Middle of the front edge.</summary>
        public Vector2 Front { get; }

        /// <summary>Unit direction along the street.</summary>
        public Vector2 Run { get; }

        /// <summary>Unit direction from the street into the plot.</summary>
        public Vector2 Back { get; }

        public float Width { get; }

        public float Depth { get; }

        /// <summary>Where the yard ends and the garden starts, as a depth from the front.</summary>
        public float YardEnd { get; set; }

        /// <summary>Where the garden ends and the back starts, as a depth from the front.</summary>
        public float GardenEnd { get; set; }

        public Vector2 Center => Point(0f, Depth * 0.5f);

        /// <summary>Yaw of something that faces the street.</summary>
        public float FacingYaw => MapGeometry.Yaw(-Back);

        public float RunYaw => MapGeometry.Yaw(Run);

        public Vector2 Point(float u, float v) => Front + Run * u + Back * v;

        public void Local(Vector2 point, out float u, out float v)
        {
            var offset = point - Front;
            u = Vector2.Dot(offset, Run);
            v = Vector2.Dot(offset, Back);
        }

        /// <summary>Whether a point lies in the plot, <paramref name="inset"/> metres in from every edge.</summary>
        public bool Contains(Vector2 point, float inset = 0f)
        {
            Local(point, out var u, out var v);
            var sideways = Mathf.Abs(u) <= Width * 0.5f - inset;
            var along = v >= inset && v <= Depth - inset;
            return sideways && along;
        }

        public Vector2[] Corners()
        {
            var half = Width * 0.5f;
            return new[] { Point(-half, 0f), Point(half, 0f), Point(half, Depth), Point(-half, Depth) };
        }

        /// <summary>The map-space box of this plot in one copy of the sector.</summary>
        public Rect Bounds(MapTransform copy)
        {
            var corners = Corners();
            for (var i = 0; i < corners.Length; i++)
            {
                corners[i] = copy.Apply(corners[i]);
            }

            return MapGeometry.Bounds(corners);
        }

        public MapPlotZone ZoneAt(float v)
        {
            if (v < YardEnd)
            {
                return MapPlotZone.Yard;
            }

            return v < GardenEnd ? MapPlotZone.Garden : MapPlotZone.Back;
        }
    }
}
