using System;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>
    /// How many frames of the playable area, each the size the match camera sees, hold enough kinds of interest: a wood
    /// edge, water, a field pattern, a built place (settlement, base yard or prop pad) and a change in relief.
    /// </summary>
    [Serializable]
    public sealed class MapComposition
    {
        private const int KindCount = 5;
        private const int WoodEdge = 1;
        private const int Water = 2;
        private const int FieldPattern = 4;
        private const int Settlement = 8;
        private const int Relief = 16;

        /// <summary>Frames tiled over the playable area.</summary>
        public int frames;

        /// <summary>Share of frames holding at least the rules' minimum kinds.</summary>
        public float share;

        /// <summary>Share of frames holding each kind.</summary>
        public float woodEdgeShare;
        public float waterShare;
        public float fieldPatternShare;
        public float settlementShare;
        public float reliefShare;

        public static MapComposition Measure(MapLand land, MapHeights heights, Rect area, MapRules rules)
        {
            var size = rules.compositionFrame;
            var counts = new int[KindCount];
            var enough = 0;
            var frames = 0;
            for (var y = area.yMin; y + size.y <= area.yMax + 0.01f; y += size.y)
            {
                for (var x = area.xMin; x + size.x <= area.xMax + 0.01f; x += size.x)
                {
                    var kinds = Kinds(land, heights, new Rect(x, y, size.x, size.y), rules.compositionMinRelief);
                    var held = 0;
                    for (var i = 0; i < KindCount; i++)
                    {
                        var has = (kinds >> i & 1) == 1;
                        counts[i] += has ? 1 : 0;
                        held += has ? 1 : 0;
                    }

                    enough += held >= rules.compositionMinKinds ? 1 : 0;
                    frames++;
                }
            }

            if (frames == 0)
            {
                return new MapComposition();
            }

            return new MapComposition
            {
                frames = frames,
                share = (float)enough / frames,
                woodEdgeShare = (float)counts[0] / frames,
                waterShare = (float)counts[1] / frames,
                fieldPatternShare = (float)counts[2] / frames,
                settlementShare = (float)counts[3] / frames,
                reliefShare = (float)counts[4] / frames,
            };
        }

        /// <summary>Warns when fewer frames than the rules' target hold enough kinds of interest.</summary>
        public void Warn(MapRules rules, MapIssues issues)
        {
            if (frames > 0 && share < rules.compositionTargetShare)
            {
                issues.Warn($"{share:P0} of {frames} camera frames hold {rules.compositionMinKinds} kinds of interest (wood edge, water, field pattern, built place, relief); the rules want {rules.compositionTargetShare:P0}.");
            }
        }

        private static int Kinds(MapLand land, MapHeights heights, Rect frame, float minRelief)
        {
            var kinds = 0;
            // ForEachIn also visits the cell past each edge, which belongs to the next frame.
            land.ForEachIn(frame, (x, z, at) =>
            {
                if (frame.Contains(at))
                {
                    kinds |= ShareIndex(land.Region(x, z)?.Kind ?? MapLandKind.Open);
                }
            });
            var (low, high) = (float.MaxValue, float.MinValue);
            heights.ForEachIn(frame, (x, z, at) =>
            {
                if (!frame.Contains(at))
                {
                    return;
                }

                low = Mathf.Min(low, heights[x, z]);
                high = Mathf.Max(high, heights[x, z]);
            });

            return high - low >= minRelief ? kinds | Relief : kinds;
        }

        private static int ShareIndex(MapLandKind kind)
        {
            switch (kind)
            {
                case MapLandKind.WoodEdge:
                case MapLandKind.Belt:
                    return WoodEdge;
                case MapLandKind.Water:
                case MapLandKind.Bank:
                case MapLandKind.Floodplain:
                    return Water;
                case MapLandKind.Field:
                case MapLandKind.Margin:
                    return FieldPattern;
                case MapLandKind.Base:
                case MapLandKind.Pad:
                case MapLandKind.Yard:
                case MapLandKind.Garden:
                case MapLandKind.Back:
                case MapLandKind.Area:
                    return Settlement;
                default:
                    return 0;
            }
        }
    }
}
