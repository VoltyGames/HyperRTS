using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Hints;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Metrics;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Simulation.AI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Maps.Build
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
        /// <paramref name="issues"/>; returns the written object, or null when no hints could be computed.
        /// </summary>
        public static AIMapHintsAuthoring Write(Scene entities, MapLayout layout, MapShapes shapes,
            IEnumerable<MapPlacement> placements, MapNavGrid nav, MapPalette palette, MapRules rules, MapIssues issues)
        {
            var input = new MapHintInput(nav, layout.Spawns, Nodes(placements), Crossings(shapes), palette.baseRadius,
                rules.corridorWidth);
            var go = MapSceneWriter.CreateEntityObject(entities, "AI Hints");
            var authoring = go.AddComponent<AIMapHintsAuthoring>();
            if (MapHintBuilder.Fill(authoring, input, issues))
            {
                EditorUtility.SetDirty(authoring);
                return authoring;
            }

            Object.DestroyImmediate(go);
            return null;
        }

        private static List<(Vector2 Position, int Owner)> Nodes(IEnumerable<MapPlacement> placements) =>
            placements.Where(p => p.Entry.IsResource).Select(p => (p.Position, p.Owner)).ToList();

        private static List<(Vector2 Position, float Radius)> Crossings(MapShapes shapes) =>
            shapes.Crossings.Select(c => (c.Position, Mathf.Max(c.Span, c.Width) * 0.5f + CrossingMargin)).ToList();
    }
}
