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

        /// <summary>Surface height units cross at: a bridge deck, or just under the water over a ford.</summary>
        public float Height { get; set; }

        public bool IsBridge => Kind == MapCrossingSpec.Bridge;

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
