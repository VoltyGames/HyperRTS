using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's active tool and, per drawing tool, the feature kind it adds. Shared by the toolbar's tool
    /// strip and the canvas, and kept when the canvas is rebuilt for another spec.
    /// </summary>
    public sealed class MapToolbox
    {
        private readonly Dictionary<MapToolMode, MapFeatureKind> _kinds = new();

        public MapToolbox()
        {
            foreach (MapToolMode mode in Enum.GetValues(typeof(MapToolMode)))
            {
                var kinds = KindsFor(mode);
                if (kinds.Count > 0)
                {
                    _kinds[mode] = kinds[kinds.Count - 1];
                }
            }
        }

        public event Action Changed;

        public MapToolMode Mode { get; private set; }

        /// <summary>The kind the active drawing tool adds; null for Select.</summary>
        public MapFeatureKind Kind => _kinds.TryGetValue(Mode, out var kind) ? kind : null;

        /// <summary>The feature kinds a tool adds: points and circles, lines, or areas; none for Select.</summary>
        public static IReadOnlyList<MapFeatureKind> KindsFor(MapToolMode mode) =>
            MapFeatureKinds.All.Where(kind => Draws(mode, kind.Geometry)).ToList();

        public void SetMode(MapToolMode mode)
        {
            if (mode == Mode)
            {
                return;
            }

            Mode = mode;
            Changed?.Invoke();
        }

        public void SetKind(MapFeatureKind kind)
        {
            if (!KindsFor(Mode).Contains(kind))
            {
                throw new ArgumentException($"The {Mode} tool can't add '{kind.Field}'.");
            }

            _kinds[Mode] = kind;
            Changed?.Invoke();
        }

        private static bool Draws(MapToolMode mode, MapFeatureGeometry geometry) => mode switch
        {
            MapToolMode.Point => geometry == MapFeatureGeometry.Point || geometry == MapFeatureGeometry.Circle,
            MapToolMode.Line => geometry == MapFeatureGeometry.Polyline,
            MapToolMode.Area => geometry == MapFeatureGeometry.Polygon,
            _ => false,
        };
    }
}
