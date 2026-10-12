using System;
using System.Linq;
using System.Text;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Reads and writes <c>&lt;Map&gt;.map.json</c>: JsonUtility's output re-indented by two spaces, with point objects
    /// and short scalar arrays on one line and whole floats written without ".0", so saves diff cleanly.
    /// </summary>
    public static class MapSpecJson
    {
        private const string Indent = "  ";
        private const int MaxInlineArray = 100;
        private const int MaxInlinePoint = 3;

        public static MapSpec Read(string json)
        {
            var spec = JsonUtility.FromJson<MapSpec>(json);
            if (spec == null)
            {
                throw new FormatException("Map spec JSON is empty.");
            }

            return spec;
        }

        public static string Write(MapSpec spec) => Format(JsonUtility.ToJson(spec, false));

        /// <summary>Lays out any JSON text the way specs are written.</summary>
        public static string Format(string json)
        {
            var output = new StringBuilder();
            Append(output, MapJsonParser.Parse(json), 0);
            return output.Append('\n').ToString();
        }

        private static void Append(StringBuilder output, MapJsonNode node, int depth)
        {
            var inline = Inline(node);
            if (inline != null && Fits(output, node, inline))
            {
                output.Append(inline);
                return;
            }

            if (node.Kind == MapJsonKind.Object)
            {
                AppendObject(output, node, depth);
                return;
            }

            AppendArray(output, node, depth);
        }

        private static void AppendObject(StringBuilder output, MapJsonNode node, int depth)
        {
            output.Append('{');
            for (var i = 0; i < node.Members.Count; i++)
            {
                NewLine(output, depth + 1);
                output.Append(node.Members[i].Key).Append(": ");
                Append(output, node.Members[i].Value, depth + 1);
                output.Append(i < node.Members.Count - 1 ? "," : string.Empty);
            }

            NewLine(output, depth);
            output.Append('}');
        }

        private static void AppendArray(StringBuilder output, MapJsonNode node, int depth)
        {
            output.Append('[');
            for (var i = 0; i < node.Items.Count; i++)
            {
                NewLine(output, depth + 1);
                Append(output, node.Items[i], depth + 1);
                output.Append(i < node.Items.Count - 1 ? "," : string.Empty);
            }

            NewLine(output, depth);
            output.Append(']');
        }

        /// <summary>The node on one line when it is a scalar, empty, a point or a scalar array; otherwise null.</summary>
        private static string Inline(MapJsonNode node)
        {
            if (!node.IsContainer)
            {
                return Number(node);
            }

            if (node.Kind == MapJsonKind.Object)
            {
                return node.Members.Count == 0 ? "{}" : InlinePoint(node);
            }

            if (node.Items.Count == 0)
            {
                return "[]";
            }

            var scalars = node.Items.All(item => !item.IsContainer);
            return scalars ? "[" + string.Join(", ", node.Items.Select(Number)) + "]" : null;
        }

        private static string InlinePoint(MapJsonNode node)
        {
            var numeric = node.Members.All(member => member.Value.IsNumber);
            if (!numeric || node.Members.Count > MaxInlinePoint)
            {
                return null;
            }

            var members = node.Members.Select(member => member.Key + ": " + Number(member.Value));
            return "{ " + string.Join(", ", members) + " }";
        }

        /// <summary>Whole floats lose their ".0" (20.0 → 20); anything else is kept as written.</summary>
        private static string Number(MapJsonNode node)
        {
            var text = node.Text;
            var whole = node.IsNumber && text.EndsWith(".0", StringComparison.Ordinal);
            return whole ? text.Substring(0, text.Length - 2) : text;
        }

        /// <summary>Scalars, points and empty containers always stay inline; scalar arrays only while the line is short.</summary>
        private static bool Fits(StringBuilder output, MapJsonNode node, string inline)
        {
            if (node.Kind != MapJsonKind.Array)
            {
                return true;
            }

            return output.Length - LineStart(output) + inline.Length <= MaxInlineArray;
        }

        private static void NewLine(StringBuilder output, int depth)
        {
            output.Append('\n');
            for (var i = 0; i < depth; i++)
            {
                output.Append(Indent);
            }
        }

        private static int LineStart(StringBuilder output)
        {
            for (var i = output.Length - 1; i >= 0; i--)
            {
                if (output[i] == '\n')
                {
                    return i + 1;
                }
            }

            return 0;
        }

        /// <summary>Equal when both specs serialize to the same JSON.</summary>
        public static bool SameContent(MapSpec a, MapSpec b) => JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
    }
}
