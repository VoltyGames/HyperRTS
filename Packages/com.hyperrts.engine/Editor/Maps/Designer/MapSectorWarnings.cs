using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Rings every authored point that lies outside the symmetry's sector in the warning colour: the symmetry copies
    /// it into another sector, so it overlaps its own copies. Shared features are placed once and are exempt.
    /// </summary>
    public static class MapSectorWarnings
    {
        private const float RingRadius = 8f;
        private const float RingWidth = 2f;

        private static readonly Color Warning = new(1f, 0.55f, 0.1f);
        [NoAutoStaticsCleanup]
        private static readonly List<Vector2> Points = new();

        public static void Draw(Painter2D painter, MapSpec spec, MapCanvasView view, MapSymmetry symmetry)
        {
            painter.strokeColor = Warning;
            painter.lineWidth = RingWidth;
            foreach (var kind in MapFeatureKinds.All)
            {
                for (var i = 0; i < kind.Count(spec); i++)
                {
                    if (!kind.IsShared(spec, i))
                    {
                        DrawOutside(painter, kind, spec, i, view, symmetry);
                    }
                }
            }
        }

        private static void DrawOutside(Painter2D painter, MapFeatureKind kind, MapSpec spec, int index,
            MapCanvasView view, MapSymmetry symmetry)
        {
            kind.ReadPoints(spec, index, Points);
            foreach (var point in Points)
            {
                if (symmetry.InSector(point))
                {
                    continue;
                }

                MapFeaturePainter.Circle(painter, view.ToLocal(point), RingRadius);
                painter.Stroke();
            }
        }
    }
}
