using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace HyperRTS.Editor.Common
{
    /// <summary>
    /// One FBX node's custom properties as an import postprocessor gets them, read by name. Integers, decimals, strings
    /// and three-number vectors come through; a missing or mistyped one throws naming the node.
    /// </summary>
    public sealed class FbxProperties
    {
        private readonly string _node;
        private readonly string[] _names;
        private readonly object[] _values;

        public FbxProperties(string node, string[] names, object[] values)
        {
            _node = node;
            _names = names;
            _values = values;
        }

        public bool Has(string name) => Array.IndexOf(_names, name) >= 0;

        public bool TryInt(string name, out int value)
        {
            var found = TryFind(name, out var raw) && raw is int or long or float or double;
            value = found ? Convert.ToInt32(raw, CultureInfo.InvariantCulture) : 0;
            return found;
        }

        public bool TryString(string name, out string value)
        {
            value = TryFind(name, out var raw) ? raw as string : null;
            return value != null;
        }

        public int Int(string name) => TryInt(name, out var value) ? value : throw Missing(name, "a whole number");

        public float Float(string name) => TryFind(name, out var raw) && raw is int or long or float or double
            ? Convert.ToSingle(raw, CultureInfo.InvariantCulture)
            : throw Missing(name, "a number");

        public bool Bool(string name) => Int(name) != 0;

        public string String(string name) => TryString(name, out var value) ? value : throw Missing(name, "text");

        /// <summary>A comma-separated list; empty text is an empty list.</summary>
        public string[] List(string name)
        {
            var text = String(name);
            return text.Length == 0 ? Array.Empty<string>() : text.Split(',');
        }

        public Vector3 Vector(string name)
        {
            if (TryFind(name, out var raw))
            {
                switch (raw)
                {
                    case Vector4 v4:
                        return v4;
                    case Vector3 v3:
                        return v3;
                    case Color color:
                        return new Vector3(color.r, color.g, color.b);
                }
            }

            throw Missing(name, "a vector");
        }

        private bool TryFind(string name, out object value)
        {
            var at = Array.IndexOf(_names, name);
            value = at >= 0 ? _values[at] : null;
            return at >= 0;
        }

        private InvalidDataException Missing(string name, string kind) =>
            new($"HyperRTS: FBX node '{_node}' needs '{name}' as {kind}.");
    }
}
