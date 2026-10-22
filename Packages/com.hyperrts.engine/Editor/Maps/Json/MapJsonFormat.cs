using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Json
{
    /// <summary>
    /// The one layout map specs and library files are written in: indented by two spaces, point objects and short
    /// scalar arrays on one line, floats in their shortest form, so saves diff cleanly. Parses with Newtonsoft,
    /// keeping member order and strings as strings (no date guessing).
    /// </summary>
    public static class MapJsonFormat
    {
        private const string Indent = "  ";
        private const int MaxInlineArray = 100;
        private const int MaxInlinePoint = 3;

        /// <summary>Parses one JSON value; malformed or trailing text throws a <see cref="FormatException"/>.</summary>
        public static JToken Parse(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json))
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double,
            };
            try
            {
                var token = JToken.ReadFrom(reader);
                if (reader.Read())
                {
                    throw new FormatException($"JSON: trailing text at line {reader.LineNumber}, position {reader.LinePosition}.");
                }

                return token;
            }
            catch (JsonException e)
            {
                throw new FormatException($"JSON: {e.Message}", e);
            }
        }

        /// <summary>Lays out any JSON text.</summary>
        public static string Format(string json) => Write(Parse(json));

        /// <summary>Lays out a value as a file, ending in a newline.</summary>
        public static string Write(JToken token)
        {
            var output = new StringBuilder();
            Append(output, token, 0);
            return output.Append('\n').ToString();
        }

        /// <summary>
        /// Overwrites <paramref name="target"/> from <paramref name="json"/> and rejects any field its own round trip
        /// lacks; see <see cref="ReadInto(string, string, string, Func{JToken}, object[])"/>.
        /// </summary>
        public static JToken ReadInto(string json, object target, string what, string source) =>
            ReadInto(json, what, source, () => Parse(JsonUtility.ToJson(target)), target);

        /// <summary>
        /// Overwrites each of <paramref name="targets"/> from <paramref name="json"/>, then rejects any field the tree
        /// <paramref name="known"/> builds from them lacks. Every problem throws an <see cref="InvalidOperationException"/>
        /// naming <paramref name="source"/>. Returns the parsed root.
        /// </summary>
        public static JToken ReadInto(string json, string what, string source, Func<JToken> known, params object[] targets)
        {
            JToken root;
            try
            {
                root = Parse(json);
                foreach (var target in targets)
                {
                    JsonUtility.FromJsonOverwrite(json, target);
                }
            }
            catch (Exception e) when (e is ArgumentException or FormatException or InvalidOperationException)
            {
                throw new InvalidOperationException($"HyperRTS: {source} is not valid JSON: {e.Message}", e);
            }

            if (root is not JObject)
            {
                throw new InvalidOperationException($"HyperRTS: {source} must be a JSON object, not {root.Type}.");
            }

            RejectUnknown(root, known(), what, source);
            return root;
        }

        /// <summary>
        /// Throws on a field, at any depth, that <paramref name="known"/> (the round trip of what was read) lacks or
        /// reads in another shape: JsonUtility drops both silently, so a typo or an old format would vanish.
        /// </summary>
        public static void RejectUnknown(JToken read, JToken known, string what, string source, string at = "")
        {
            var reshaped = known is JContainer && read.Type != known.Type && read.Type != JTokenType.Null;
            if (reshaped)
            {
                var shape = known is JObject ? "an object" : "a list";
                throw new InvalidOperationException($"HyperRTS: {source}: '{at}' must be {shape}, not {read.Type}.");
            }

            if (read is JArray array && known is JArray knownArray)
            {
                for (var i = 0; i < Math.Min(array.Count, knownArray.Count); i++)
                {
                    RejectUnknown(array[i], knownArray[i], what, source, $"{at}[{i}]");
                }

                return;
            }

            if (read is not JObject obj || known is not JObject knownObj)
            {
                return;
            }

            foreach (var property in obj.Properties())
            {
                var path = at.Length == 0 ? property.Name : $"{at}.{property.Name}";
                if (!knownObj.TryGetValue(property.Name, out var value))
                {
                    throw new InvalidOperationException($"HyperRTS: {source}: '{path}' is not a {what} field.");
                }

                RejectUnknown(property.Value, value, what, source, path);
            }
        }

        private static void Append(StringBuilder output, JToken token, int depth)
        {
            var inline = Inline(token);
            if (inline != null && Fits(output, token, inline))
            {
                output.Append(inline);
                return;
            }

            if (token is JObject obj)
            {
                AppendObject(output, obj, depth);
                return;
            }

            AppendArray(output, (JArray)token, depth);
        }

        private static void AppendObject(StringBuilder output, JObject obj, int depth)
        {
            output.Append('{');
            var properties = obj.Properties().ToList();
            for (var i = 0; i < properties.Count; i++)
            {
                NewLine(output, depth + 1);
                output.Append(JsonConvert.ToString(properties[i].Name)).Append(": ");
                Append(output, properties[i].Value, depth + 1);
                output.Append(i < properties.Count - 1 ? "," : string.Empty);
            }

            NewLine(output, depth);
            output.Append('}');
        }

        private static void AppendArray(StringBuilder output, JArray array, int depth)
        {
            output.Append('[');
            for (var i = 0; i < array.Count; i++)
            {
                NewLine(output, depth + 1);
                Append(output, array[i], depth + 1);
                output.Append(i < array.Count - 1 ? "," : string.Empty);
            }

            NewLine(output, depth);
            output.Append(']');
        }

        /// <summary>The value on one line when it is a scalar, empty, a point or a scalar array; otherwise null.</summary>
        private static string Inline(JToken token)
        {
            if (token is JObject obj)
            {
                return obj.Count == 0 ? "{}" : InlinePoint(obj);
            }

            if (token is not JArray array)
            {
                return Scalar(token);
            }

            if (array.Count == 0)
            {
                return "[]";
            }

            var scalars = array.All(item => item is JValue);
            return scalars ? "[" + string.Join(", ", array.Select(Scalar)) + "]" : null;
        }

        private static string InlinePoint(JObject obj)
        {
            var numeric = obj.Properties().All(property => IsNumber(property.Value));
            if (!numeric || obj.Count > MaxInlinePoint)
            {
                return null;
            }

            var members = obj.Properties().Select(property => JsonConvert.ToString(property.Name) + ": " + Scalar(property.Value));
            return "{ " + string.Join(", ", members) + " }";
        }

        /// <summary>
        /// A scalar's JSON text. Map data is single precision, so a float is written in the shortest form that reads
        /// back as the same float: 20.0 → 20, and JsonUtility's 0.34999999403953552 → 0.35.
        /// </summary>
        private static string Scalar(JToken token) => token.Type == JTokenType.Float
            ? token.Value<float>().ToString("R", CultureInfo.InvariantCulture)
            : token.ToString(Formatting.None);

        private static bool IsNumber(JToken token) => token.Type is JTokenType.Integer or JTokenType.Float;

        /// <summary>Scalars, points and empty containers always stay inline; scalar arrays only while the line is short.</summary>
        private static bool Fits(StringBuilder output, JToken token, string inline)
        {
            if (token is not JArray)
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
    }
}
