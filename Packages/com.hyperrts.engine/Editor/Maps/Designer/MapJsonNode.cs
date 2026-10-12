using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>A parsed JSON value kept as written: an object's members in order, an array's items, or a scalar's text.</summary>
    public sealed class MapJsonNode
    {
        private MapJsonNode(MapJsonKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public MapJsonKind Kind { get; }

        /// <summary>A scalar's JSON text (number, quoted string, true, false, null).</summary>
        public string Text { get; }

        public List<KeyValuePair<string, MapJsonNode>> Members { get; } = new();

        public List<MapJsonNode> Items { get; } = new();

        public bool IsContainer => Kind != MapJsonKind.Scalar;

        public bool IsNumber => Kind == MapJsonKind.Scalar && Text.Length > 0 && (char.IsDigit(Text[0]) || Text[0] == '-');

        public static MapJsonNode Object() => new(MapJsonKind.Object, null);

        public static MapJsonNode Array() => new(MapJsonKind.Array, null);

        public static MapJsonNode Scalar(string text) => new(MapJsonKind.Scalar, text);
    }
}
