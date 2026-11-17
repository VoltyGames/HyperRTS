using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>
    /// A JSON object of a staging file, read key by key. <see cref="Finish"/> fails on a key nobody read, so a typo
    /// never passes silently, and every failure names the path of the key.
    /// </summary>
    internal sealed class StagingObject
    {
        private readonly JObject _json;
        private readonly string _path;
        private readonly HashSet<string> _read = new();
        private readonly List<StagingObject> _children = new();

        public StagingObject(JToken token, string path)
        {
            _path = path;
            _json = token as JObject ?? throw Problem($"must be an object, not {token.Type}");
        }

        public static StagingObject Parse(string json, string source)
        {
            try
            {
                return new StagingObject(JToken.Parse(json), source);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"HyperRTS: staging {source} is not valid JSON: {exception.Message}", exception);
            }
        }

        public bool Has(string key) => _json.ContainsKey(key);

        public string Text(string key) => (string)Read(key, JTokenType.String);

        public float Number(string key) => (float)Read(key, JTokenType.Integer, JTokenType.Float);

        public int Whole(string key) => (int)Read(key, JTokenType.Integer);

        public bool Flag(string key) => (bool)Read(key, JTokenType.Boolean);

        /// <summary>A number or a string, as the key allows both.</summary>
        public JToken Either(string key) => Read(key, JTokenType.Integer, JTokenType.Float, JTokenType.String);

        /// <summary>A map point written <c>[x, z]</c>.</summary>
        public float2 Point(string key)
        {
            var list = (JArray)Read(key, JTokenType.Array);
            var numbers = list.All(item => item.Type is JTokenType.Integer or JTokenType.Float);
            if (list.Count != 2 || !numbers)
            {
                throw Problem($"'{key}' must be two numbers [x, z]");
            }

            return new float2((float)list[0], (float)list[1]);
        }

        public StagingObject Child(string key) => Track(new StagingObject(Read(key, JTokenType.Object), $"{_path}.{key}"));

        public List<StagingObject> Items(string key)
        {
            var list = (JArray)Read(key, JTokenType.Array);
            return list.Select((item, i) => Track(new StagingObject(item, $"{_path}.{key}[{i}]"))).ToList();
        }

        /// <summary>The one of <paramref name="keys"/> this object has; none or two fails.</summary>
        public string Choose(IReadOnlyCollection<string> keys)
        {
            var found = keys.Where(Has).ToList();
            if (found.Count == 0)
            {
                throw Problem($"needs one of {Quote(keys)}");
            }

            return found.Count == 1 ? found[0] : throw Problem($"has {Quote(found)} together; give one");
        }

        /// <summary>Fails on a key no reader took, here and in every child.</summary>
        public void Finish()
        {
            var unknown = _json.Properties().Select(property => property.Name).FirstOrDefault(key => !_read.Contains(key));
            if (unknown != null)
            {
                throw Problem($"has an unknown key '{unknown}'");
            }

            foreach (var child in _children)
            {
                child.Finish();
            }
        }

        public InvalidOperationException Problem(string problem) => new($"HyperRTS: staging {_path} {problem}.");

        private static string Quote(IEnumerable<string> keys) => string.Join(", ", keys.Select(key => $"'{key}'"));

        private StagingObject Track(StagingObject child)
        {
            _children.Add(child);
            return child;
        }

        private JToken Read(string key, params JTokenType[] types)
        {
            if (!_json.TryGetValue(key, out var token))
            {
                throw Problem($"needs '{key}'");
            }

            _read.Add(key);
            return types.Contains(token.Type) ? token : throw Problem($"'{key}' must be {Describe(types)}, not {token.Type}");
        }

        private static string Describe(JTokenType[] types) =>
            string.Join(" or ", types.Select(type => type == JTokenType.Float ? "decimal" : type.ToString().ToLowerInvariant()));
    }
}
