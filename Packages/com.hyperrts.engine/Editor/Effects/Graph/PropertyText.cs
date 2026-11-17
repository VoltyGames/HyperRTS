using System;
using System.Globalization;
using System.Linq;
using HyperRTS.Editor.Effects.Layer;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Graph
{
    /// <summary>
    /// A recipe field read, written and scaled as command-line text: numbers, names, colours as <c>r,g,b[,a]</c>, vectors
    /// as <c>x,y,z</c>, ranges as <c>min..max</c>, curves as <c>t:v</c> pairs, textures by asset name or path, and bursts.
    /// </summary>
    internal sealed class PropertyText
    {
        private readonly SerializedProperty _property;

        public PropertyText(SerializedProperty property) => _property = property;

        public static string Text(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        public static string Text(Color color) => $"{Text(color.r)},{Text(color.g)},{Text(color.b)},{Text(color.a)}";

        public static string Text(Vector3 vector) => $"{Text(vector.x)},{Text(vector.y)},{Text(vector.z)}";

        public static string Text(FloatRange range) => range.IsConstant ? Text(range.min) : $"{Text(range.min)}..{Text(range.max)}";

        public static string Text(AnimationCurve curve) =>
            string.Join(",", curve.keys.Select(key => $"{Text(key.time)}:{Text(key.value)}"));

        public static string Text(EffectBurst burst)
        {
            var text = $"{Text(burst.delay)}:{Text(burst.count.min)}-{Text(burst.count.max)}";
            return burst.repeats > 0 ? $"{text}x{burst.repeats}/{Text(burst.interval)}" : text;
        }

        public static float ParseFloat(string text, string field)
        {
            if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new ArgumentException($"HyperRTS: '{text}' is not a number for '{field}'.");
            }

            return value;
        }

        public string Read()
        {
            switch (_property.propertyType)
            {
                case SerializedPropertyType.Float: return Text(_property.floatValue);
                case SerializedPropertyType.Integer: return _property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean: return _property.boolValue ? "true" : "false";
                case SerializedPropertyType.String: return _property.stringValue;
                case SerializedPropertyType.Enum: return _property.enumNames[_property.enumValueIndex];
                case SerializedPropertyType.Color: return Text(_property.colorValue);
                case SerializedPropertyType.Vector3: return Text(_property.vector3Value);
                case SerializedPropertyType.AnimationCurve: return Text(_property.animationCurveValue);
                case SerializedPropertyType.ObjectReference:
                    return _property.objectReferenceValue == null ? "none" : _property.objectReferenceValue.name;
                case SerializedPropertyType.Generic when IsRange: return Text(Range());
                case SerializedPropertyType.Generic when _property.isArray: return Bursts().Read();
                default: throw Unsupported();
            }
        }

        public void Write(string text)
        {
            switch (_property.propertyType)
            {
                case SerializedPropertyType.Float: _property.floatValue = ParseFloat(text, Field()); break;
                case SerializedPropertyType.Integer: _property.intValue = Mathf.RoundToInt(ParseFloat(text, Field())); break;
                case SerializedPropertyType.Boolean: _property.boolValue = ParseBool(text); break;
                case SerializedPropertyType.String: _property.stringValue = text; break;
                case SerializedPropertyType.Enum: _property.enumValueIndex = EnumIndex(text); break;
                case SerializedPropertyType.Color: _property.colorValue = ParseColor(text); break;
                case SerializedPropertyType.Vector3: _property.vector3Value = ParseVector(text); break;
                case SerializedPropertyType.AnimationCurve: _property.animationCurveValue = ParseCurve(text); break;
                case SerializedPropertyType.ObjectReference: _property.objectReferenceValue = Asset(text); break;
                case SerializedPropertyType.Generic when IsRange: WriteRange(text); break;
                case SerializedPropertyType.Generic when _property.isArray: Bursts().Write(text); break;
                default: throw Unsupported();
            }
        }

        public void Scale(float factor)
        {
            switch (_property.propertyType)
            {
                case SerializedPropertyType.Float: _property.floatValue *= factor; break;
                case SerializedPropertyType.Integer: _property.intValue = Mathf.RoundToInt(_property.intValue * factor); break;
                case SerializedPropertyType.Generic when IsRange:
                    _property.FindPropertyRelative("min").floatValue *= factor;
                    _property.FindPropertyRelative("max").floatValue *= factor;
                    break;
                default: throw new ArgumentException($"HyperRTS: '{Field()}' is a {_property.propertyType} and cannot be scaled; use a number or a range.");
            }
        }

        private string Field() => _property.propertyPath;

        private bool IsRange =>
            _property.propertyType == SerializedPropertyType.Generic
            && _property.FindPropertyRelative("min")?.propertyType == SerializedPropertyType.Float
            && _property.FindPropertyRelative("max")?.propertyType == SerializedPropertyType.Float;

        private BurstText Bursts() => new(_property);

        private FloatRange Range() =>
            new(_property.FindPropertyRelative("min").floatValue, _property.FindPropertyRelative("max").floatValue);

        private void WriteRange(string text)
        {
            var ends = text.Split("..");
            if (ends.Length > 2)
            {
                throw new ArgumentException($"HyperRTS: '{text}' is not a range for '{Field()}'; write 'min..max' or one number.");
            }

            var min = ParseFloat(ends[0], Field());
            _property.FindPropertyRelative("min").floatValue = min;
            _property.FindPropertyRelative("max").floatValue = ends.Length == 2 ? ParseFloat(ends[1], Field()) : min;
        }

        private ArgumentException Unsupported() =>
            new($"HyperRTS: '{Field()}' is a {_property.propertyType} group or list; set one of its fields, such as '{Field()}.min'.");

        private bool ParseBool(string text) =>
            text.Trim().ToLowerInvariant() switch
            {
                "true" => true,
                "false" => false,
                _ => throw new ArgumentException($"HyperRTS: '{text}' is not true or false for '{Field()}'."),
            };

        private int EnumIndex(string text)
        {
            var names = _property.enumNames;
            var index = Array.FindIndex(names, name => string.Equals(name, text.Trim(), StringComparison.OrdinalIgnoreCase));
            return index >= 0 ? index : throw new ArgumentException($"HyperRTS: '{text}' is not a value of '{Field()}': {string.Join(", ", names)}.");
        }

        private Color ParseColor(string text)
        {
            var parts = Floats(text, 3, 4);
            return new Color(parts[0], parts[1], parts[2], parts.Length == 4 ? parts[3] : 1f);
        }

        private Vector3 ParseVector(string text)
        {
            var parts = Floats(text, 3, 3);
            return new Vector3(parts[0], parts[1], parts[2]);
        }

        private float[] Floats(string text, int least, int most)
        {
            var parts = text.Split(',').Select(part => ParseFloat(part, Field())).ToArray();
            return parts.Length >= least && parts.Length <= most
                ? parts
                : throw new ArgumentException($"HyperRTS: '{text}' needs {(least == most ? least.ToString() : $"{least} or {most}")} comma separated numbers for '{Field()}'.");
        }

        // Straight lines between the points, as the builder's own frame curve.
        private AnimationCurve ParseCurve(string text)
        {
            var points = text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(pair =>
            {
                var parts = pair.Split(':');
                if (parts.Length != 2)
                {
                    throw new ArgumentException($"HyperRTS: '{pair}' is not 'time:value' for '{Field()}'.");
                }

                return (Time: ParseFloat(parts[0], Field()), Value: ParseFloat(parts[1], Field()));
            }).OrderBy(point => point.Time).ToArray();
            var keys = new Keyframe[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var before = i > 0 ? Slope(points[i - 1], points[i]) : 0f;
                var after = i < points.Length - 1 ? Slope(points[i], points[i + 1]) : 0f;
                keys[i] = new Keyframe(points[i].Time, points[i].Value, i > 0 ? before : after, i < points.Length - 1 ? after : before);
            }

            return new AnimationCurve(keys);
        }

        private float Slope((float Time, float Value) from, (float Time, float Value) to) =>
            to.Time > from.Time
                ? (to.Value - from.Value) / (to.Time - from.Time)
                : throw new ArgumentException($"HyperRTS: the curve '{Field()}' needs a different time at each point.");

        private UnityEngine.Object Asset(string text)
        {
            var name = text.Trim();
            if (name is "" or "none")
            {
                return null;
            }

            var path = name.Contains('/') ? name : FindAsset(name);
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)
                ?? throw new ArgumentException($"HyperRTS: no asset at '{path}' for '{Field()}'.");
        }

        // PPtr<$Texture2D> names the field's type.
        private string FindAsset(string name)
        {
            var type = _property.type;
            var typeName = type.Substring(type.IndexOf('<') + 1).TrimEnd('>').TrimStart('$');
            var paths = AssetDatabase.FindAssets($"{name} t:{typeName}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => System.IO.Path.GetFileNameWithoutExtension(path) == name)
                .ToList();
            return paths.Count == 1
                ? paths[0]
                : throw new ArgumentException($"HyperRTS: {paths.Count} {typeName} assets are named '{name}' for '{Field()}'; give a path.{string.Concat(paths.Select(path => "\n  " + path))}");
        }
    }
}
