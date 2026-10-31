using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Nav;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Scenes;
using HyperRTS.Simulation.AI;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Hints
{
    /// <summary>
    /// Writes the map's bot hints into the entities scene: computes expansion order, attack lanes and defence points
    /// from the nav grid and layout (<see cref="MapHintBuilder"/>) and bakes them as an <see cref="AIMapHintsAuthoring"/>
    /// object, which the skirmish AI needs to play the map.
    /// </summary>
    public static class MapHintWriter
    {
        /// <summary>How far past a bridge or ford's own size a path still counts as crossing it (metres).</summary>
        private const float CrossingMargin = 4f;

        /// <summary>
        /// Call once the nav grid exists, after measuring and before the entities scene is saved. Errors go to
        /// <paramref name="issues"/>; nothing is written when no hints could be computed.
        /// </summary>
        public static void Write(MapScene scene, MapGeneration generation, MapNavGrid nav, MapIssues issues)
        {
            var project = generation.Project;
            var input = new MapHintInput(nav, generation.Layout.Spawns, Nodes(generation.Placements.All),
                Crossings(generation.Shapes), project.Palette.baseRadius, project.Rules.corridorWidth);
            var go = scene.CreateEntityObject("AI Hints");
            var authoring = go.AddComponent<AIMapHintsAuthoring>();
            if (MapHintBuilder.Fill(authoring, input, issues))
            {
                EditorUtility.SetDirty(authoring);
                return;
            }

            Object.DestroyImmediate(go);
        }

        private static List<(Vector2 Position, int Owner)> Nodes(IEnumerable<MapPlacement> placements) =>
            placements.Where(p => p.Entry.IsResource).Select(p => (p.Position, p.Owner)).ToList();

        private static List<(Vector2 Position, float Radius)> Crossings(MapShapes shapes) =>
            shapes.Crossings.Select(c => (c.Position, Mathf.Max(c.Span, c.Width) * 0.5f + CrossingMargin)).ToList();
    }
}
