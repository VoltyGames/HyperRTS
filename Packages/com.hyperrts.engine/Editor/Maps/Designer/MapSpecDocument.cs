using System;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>A map spec open in the Map Editor, saved at once through <see cref="MapSpecJson.WriteOver"/>.</summary>
    public sealed class MapSpecDocument : JsonDocument
    {
        /// <summary>The serialized field holding the spec, the root of every property path (<c>spec.roads.Array.data[3]</c>).</summary>
        public const string SpecField = nameof(spec);

        [SerializeField] private MapSpec spec = new();

        public string MapName => MapPaths.MapName(Path);

        public MapSpec Spec => spec;

        /// <summary>The document for <paramref name="specPath"/>; the caller destroys it when done.</summary>
        public static MapSpecDocument Open(string specPath)
        {
            if (!MapPaths.IsSpec(specPath))
            {
                throw new ArgumentException($"'{specPath}' is not a map spec ({MapPaths.SpecExtension}).");
            }

            return Open<MapSpecDocument>(specPath);
        }

        /// <summary>The property path of an element of a spec list, such as <c>spec.roads.Array.data[3]</c>.</summary>
        public static string ElementPath(string list, int index) => $"{SpecField}.{list}.Array.data[{index}]";

        /// <summary>Records <paramref name="edit"/> to the spec, made in code rather than through the serialized object, as one undo step, and saves.</summary>
        public void Edit(string undoName, Action<MapSpec> edit) => Record(undoName, () => edit(spec));

        protected override void Read(string json) => spec = MapSpecJson.Read(json);

        protected override string Write() => MapSpecJson.Write(spec);

        protected override string WriteOver(string fileJson) => MapSpecJson.WriteOver(fileJson, spec);

        protected override string Canonical(string fileJson) => MapSpecJson.Write(MapSpecJson.Read(fileJson));
    }
}
