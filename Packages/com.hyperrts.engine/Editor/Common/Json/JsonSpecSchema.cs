using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HyperRTS.Editor.Common.Json
{
    /// <summary>
    /// Builds a JSON Schema for a spec read with JsonUtility (map specs, effect recipes), so editors complete fields
    /// and flag unknown ones the way <see cref="JsonFormat.ReadInto(string, object, string, string)"/> rejects them.
    /// </summary>
    public sealed class JsonSpecSchema
    {
        private readonly JObject _definitions = new();
        private readonly Func<FieldInfo, IReadOnlyList<string>> _keys;

        private JsonSpecSchema(Func<FieldInfo, IReadOnlyList<string>> keys) => _keys = keys;

        /// <summary>
        /// The schema of <paramref name="root"/>: public fields become properties, lists arrays and nested classes
        /// definitions. <paramref name="keys"/> gives the values a string field may take, or null for any.
        /// </summary>
        public static JObject Build(Type root, string title, Func<FieldInfo, IReadOnlyList<string>> keys)
        {
            var builder = new JsonSpecSchema(keys);
            var schema = new JObject
            {
                ["$schema"] = "http://json-schema.org/draft-07/schema#",
                ["title"] = title,
            };
            schema.Merge(builder.Object(root));
            schema["definitions"] = builder._definitions;
            return schema;
        }

        private JObject Object(Type type)
        {
            var defaults = type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null ? Activator.CreateInstance(type) : null;
            var properties = new JObject();
            foreach (var field in SerializedFields(type))
            {
                properties[field.Name] = Field(field, defaults);
            }

            return new JObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
        }

        private JObject Field(FieldInfo field, object defaults)
        {
            var schema = Value(field.FieldType);
            var keys = field.FieldType == typeof(string) ? _keys(field) : null;
            if (keys != null)
            {
                schema["enum"] = new JArray(keys);
            }

            var value = defaults == null ? null : field.GetValue(defaults);
            if (value is bool or int or float or string)
            {
                schema["default"] = JToken.FromObject(value);
            }

            Describe(field, schema);
            return schema;
        }

        private static void Describe(FieldInfo field, JObject schema)
        {
            if (field.GetCustomAttribute<TooltipAttribute>() is { } tooltip)
            {
                schema["description"] = tooltip.tooltip;
            }

            if (field.GetCustomAttribute<MinAttribute>() is { } min)
            {
                schema["minimum"] = min.min;
            }

            if (field.GetCustomAttribute<RangeAttribute>() is { } range)
            {
                schema["minimum"] = range.min;
                schema["maximum"] = range.max;
            }
        }

        private JObject Value(Type type)
        {
            if (type == typeof(bool))
            {
                return new JObject { ["type"] = "boolean" };
            }

            if (type == typeof(int) || type.IsEnum)
            {
                return new JObject { ["type"] = "integer" };
            }

            if (type == typeof(float) || type == typeof(double))
            {
                return new JObject { ["type"] = "number" };
            }

            if (type == typeof(string))
            {
                return new JObject { ["type"] = "string" };
            }

            var item = UnitySerialization.ItemType(type);
            if (item != null)
            {
                return new JObject { ["type"] = "array", ["items"] = Value(item) };
            }

            return Reference(type);
        }

        private JObject Reference(Type type)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                throw new ArgumentException($"HyperRTS: a JSON spec can't hold the Unity object field type {type.Name}.");
            }

            if (!_definitions.ContainsKey(type.Name))
            {
                // Claimed before it is built, so a type that holds itself ends.
                _definitions[type.Name] = new JObject();
                _definitions[type.Name] = Object(type);
            }

            return new JObject { ["$ref"] = $"#/definitions/{type.Name}" };
        }

        // Base class fields first, as JsonUtility writes them.
        private static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            var chain = new List<Type>();
            for (var at = type; at != null; at = at.BaseType)
            {
                chain.Insert(0, at);
            }

            const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return chain.SelectMany(at => at.GetFields(declared)).Where(UnitySerialization.IsSerialized);
        }
    }
}
