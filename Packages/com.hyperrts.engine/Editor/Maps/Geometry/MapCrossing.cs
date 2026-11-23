using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>A bridge or ford resolved in map space: where it is, which way it spans and how high units cross.</summary>
    public sealed class MapCrossing
    {
        public string Kind { get; set; }

        /// <summary>Palette key of the bridge prefab; empty for a ford.</summary>
        public string Prop { get; set; }

        public Vector2 Position { get; set; }

        /// <summary>Unit direction from one bank to the other.</summary>
        public Vector2 Across { get; set; }

        /// <summary>Width of the crossing along the river.</summary>
        public float Width { get; set; }

        /// <summary>Length of the crossing from bank to bank.</summary>
        public float Span { get; set; }

        /// <summary>Length of level ground a bridge's prefab covers past each end of its span, as its approach slabs do.</summary>
        public float Landing { get; set; }

        /// <summary>A bridge's length between its abutment faces, where the ground keeps the river's banks; 0 for a ford.</summary>
        public float ClearSpan { get; set; }

        /// <summary>A bridge's piers in map space as (x, z, half-width across the flow); empty for a ford or a single span.</summary>
        public Vector3[] Piers { get; private set; } = System.Array.Empty<Vector3>();

        /// <summary>Surface height units cross at: a bridge deck, or just under the water over a ford.</summary>
        public float Height { get; set; }

        public bool IsBridge => Kind == MapCrossingSpec.Bridge;

        /// <summary>Depth of each of a bridge's abutments along it, from its face to the deck's end.</summary>
        public float AbutmentDepth => (Span - ClearSpan) * 0.5f;

        /// <summary>
        /// How much a bridge leaves the ground at a point as the river shaped it: 1 between its abutments over the
        /// roadway and its road shoulders, easing to 0 at the deck's ends and past the shoulders.
        /// </summary>
        public float SpanOpening(Vector2 point)
        {
            if (!IsBridge)
            {
                return 0f;
            }

            var local = Local(point);
            var along = MapGeometry.Smooth((Mathf.Abs(local.y) - ClearSpan * 0.5f) / AbutmentDepth);
            var shoulder = Width * 0.5f + MapShapes.RoadShoulder;
            var side = MapGeometry.Smooth((Mathf.Abs(local.x) - shoulder) / MapShapes.RoadShoulder);
            return (1f - along) * (1f - side);
        }

        /// <summary>Sets <see cref="Piers"/> from a deck's piers, which sit in the deck's frame: x along the river, z across it.</summary>
        public void PlacePiers(Vector3[] deckPiers)
        {
            var along = MapGeometry.Right(Across);
            Piers = new Vector3[deckPiers.Length];
            for (var i = 0; i < deckPiers.Length; i++)
            {
                var point = Position + along * deckPiers[i].x + Across * deckPiers[i].y;
                Piers[i] = new Vector3(point.x, point.y, deckPiers[i].z);
            }
        }

        public float Yaw => MapGeometry.Yaw(Across);

        /// <summary>Coordinates of a point in the crossing's frame: x along the river, y across it.</summary>
        public Vector2 Local(Vector2 point)
        {
            var offset = point - Position;
            var along = MapGeometry.Right(Across);
            return new Vector2(Vector2.Dot(offset, along), Vector2.Dot(offset, Across));
        }
    }
}
