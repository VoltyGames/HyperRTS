using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Presentation.Effects;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// Writes a rig's VFX Graph from its <see cref="EffectGraphSpec"/>: the Burst event, the Density property and one
    /// system per layer, each placed at the burst pose, thinned by Density and shaded by the fog of war. The graph is
    /// generated output, stamped with its spec so a stale one is found.
    /// </summary>
    public sealed class EffectGraphBuilder
    {
        private const string BuildMenu = "Assets/HyperRTS/Build Effect Graph";
        private const string BuildAllMenu = "Assets/HyperRTS/Build All Effect Graphs";
        private static readonly string[] PoseAttributes = { "position", "direction", "axisZ", "scale" };

        private readonly GraphModel _graph;
        private readonly object _density;
        private readonly object[] _pose;

        private EffectGraphBuilder(string graphPath)
        {
            _graph = GraphModel.Open(graphPath);
            _density = _graph.Parameter(EffectGraph.DensityProperty, 1f);
            _pose = PoseAttributes.Select((attribute, i) => _graph.SourceAttribute(attribute, new Vector2(-600f, 200f + 120f * i))).ToArray();
        }

        /// <summary>Builds the graph beside the spec at <paramref name="jsonPath"/>.</summary>
        public static void Build(string jsonPath)
        {
            var spec = EffectGraphSpec.Parse(File.ReadAllText(jsonPath), jsonPath);
            Build(spec, EffectGraphSpec.GraphPath(jsonPath), EffectGraphSpec.Stamp(jsonPath));
        }

        /// <summary>Builds <paramref name="spec"/> into the graph at <paramref name="graphPath"/>, stamped with <paramref name="stamp"/>.</summary>
        public static void Build(EffectGraphSpec spec, string graphPath, string stamp)
        {
            var builder = new EffectGraphBuilder(graphPath);
            var burst = builder._graph.Context("VFXBasicEvent", new Vector2(0f, -400f));
            builder._graph.Set(burst, "eventName", EffectGraph.BurstEvent);
            for (var i = 0; i < spec.layers.Count; i++)
            {
                new EffectLayerSystem(builder._graph, spec.layers[i], i * 520f, builder._density, builder._pose).Write(burst);
            }

            builder._graph.Save(stamp);
        }

        /// <summary>Builds every effect graph spec in the project, one graph at a time.</summary>
        public static void BuildAll()
        {
            foreach (var path in SpecPaths())
            {
                Build(path);
            }
        }

        /// <summary>Every <c>*.effect.json</c> in the project, the engine's included.</summary>
        public static IEnumerable<string> SpecPaths() =>
            AssetDatabase.FindAssets("effect t:TextAsset").Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(EffectGraphSpec.Extension, StringComparison.Ordinal)).Distinct().OrderBy(path => path);

        /// <summary>What is wrong with the graph of the spec at <paramref name="jsonPath"/>, null when it is current.</summary>
        public static string Problem(string jsonPath)
        {
            var graphPath = EffectGraphSpec.GraphPath(jsonPath);
            var importer = AssetImporter.GetAtPath(graphPath);
            if (importer == null)
            {
                return $"{jsonPath} has no graph {graphPath}; build it";
            }

            return importer.userData == EffectGraphSpec.Stamp(jsonPath) ? null : $"{graphPath} is older than {jsonPath}; rebuild it";
        }

        [MenuItem(BuildMenu)]
        private static void BuildSelected()
        {
            foreach (var path in SelectedSpecs())
            {
                Build(path);
            }
        }

        [MenuItem(BuildMenu, true)]
        private static bool CanBuildSelected() => SelectedSpecs().Any();

        [MenuItem(BuildAllMenu)]
        private static void BuildEvery() => BuildAll();

        private static IEnumerable<string> SelectedSpecs() =>
            Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(EffectGraphSpec.Extension, StringComparison.Ordinal));
    }
}
