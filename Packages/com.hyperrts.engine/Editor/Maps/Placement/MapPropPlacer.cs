using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement
{
    /// <summary>
    /// Places the spec's palette prefabs (resources, props, bridges, prop rows and scatters) clear of the start kits'
    /// ground. Random choices are made once in the authored sector and copied, so every copy matches.
    /// </summary>
    public sealed class MapPropPlacer
    {
        private const int ScatterAttempts = 40;
        private const float RoadsideSetback = MapClearance.RoadMargin + 0.5f;

        private readonly MapLayout _layout;
        private readonly MapShapes _shapes;
        private readonly MapPalette _palette;
        private readonly MapClimate _climate;
        private readonly MapIssues _issues;
        private readonly MapPlacements _placements;

        private MapPropPlacer(MapLayout layout, MapShapes shapes, MapPalette palette, MapClimate climate,
            MapIssues issues)
        {
            _layout = layout;
            _shapes = shapes;
            _palette = palette;
            _climate = climate;
            _issues = issues;
            _placements = new MapPlacements(layout, shapes, palette.baseRadius);
        }

        private MapClearance Clearance => _placements.Clearance;

        /// <summary>The spec's placements, which the land planners then add to.</summary>
        public static MapPlacements Place(MapLayout layout, MapShapes shapes, MapPalette palette, MapClimate climate,
            MapIssues issues)
        {
            var placer = new MapPropPlacer(layout, shapes, palette, climate, issues);
            placer.ReserveStartKits();
            placer.PlaceResources();
            placer.PlaceProps();
            placer.PlaceBridges();
            placer.PlaceLines();
            placer.PlaceScatters();
            return placer._placements;
        }

        /// <summary>Kits spawn in the match, not the scene; their ground is kept clear of what is placed after.</summary>
        private void ReserveStartKits()
        {
            foreach (var spawn in _layout.Spawns)
            {
                var forward = MapGeometry.Forward(spawn.Yaw);
                var right = MapGeometry.Right(forward);
                foreach (var item in _palette.sides.SelectMany(side => side.startKit))
                {
                    var position = spawn.Position + right * item.offset.x + forward * item.offset.y;
                    Clearance.Reserve(position, _palette.Entry(item.key).Radius);
                }
            }
        }

        private void PlaceResources()
        {
            foreach (var resource in _layout.Resources)
            {
                var entry = _palette.Entry(resource.Placed.kind);
                var yaw = resource.Transform.ApplyYaw(Jitter(entry, MapRandomChannel.ResourceYaw, resource.Index));
                _placements.Add(new MapPlacement(entry, resource.Placed.pos.ToVector2(), yaw, 1f) { Amount = resource.Placed.amount });
            }
        }

        private void PlaceProps()
        {
            foreach (var prop in _layout.Props)
            {
                var entry = _palette.Entry(prop.Placed.kind);
                var yaw = prop.Transform.ApplyYaw(prop.Authored.yaw + Jitter(entry, MapRandomChannel.PropYaw, prop.Index));
                var position = prop.Placed.pos.ToVector2();
                if (prop.Copy == 0)
                {
                    CheckOffRoad(entry, position, new MapIssueFeature(nameof(MapSpec.props), prop.Index));
                }

                _placements.Add(new MapPlacement(entry, position, yaw, 1f));
            }
        }

        /// <summary>A structure on its own pad must not straddle a road; a road through it reads as a mistake.</summary>
        private void CheckOffRoad(MapPaletteEntry entry, Vector2 position, MapIssueFeature feature)
        {
            var standsOnPad = entry.flatten || !string.IsNullOrEmpty(entry.pad);
            var halfWidth = Mathf.Min(entry.size.x, entry.size.y) * 0.5f;
            if (standsOnPad && _shapes.RoadEdgeDistance(position) < halfWidth)
            {
                _issues.Error($"Prop '{entry.key}' at ({position.x:0}, {position.y:0}) stands on a road; move it beside the road.", feature);
            }
        }

        private void PlaceBridges()
        {
            foreach (var crossing in _shapes.Crossings.Where(c => c.IsBridge))
            {
                _placements.Add(new MapPlacement(_palette.Entry(crossing.Prop), crossing.Position, crossing.Yaw, 1f)
                {
                    Height = crossing.Height,
                    Deck = new Vector2(crossing.Width, crossing.Span + 2f),
                });
            }
        }

        /// <summary>Prop rows; rows of trees are the land planners'.</summary>
        private void PlaceLines()
        {
            foreach (var line in _layout.Lines.Where(l => l.Copy == 0 && !_climate.HasTree(l.Authored.kind)))
            {
                var entry = _palette.Entry(line.Authored.kind);
                var points = MapGeometry.ToVectors(line.Authored.points);
                var length = MapGeometry.Length(points);
                var path = new MapPathWalker(points);
                var step = Mathf.Max(1f, line.Authored.spacing);
                for (var along = step * 0.5f; along < length; along += step)
                {
                    var position = path.PointAt(along, out var direction);
                    if (Clearance.IsDry(position, entry.Radius))
                    {
                        var yaw = MapGeometry.Yaw(direction) + Jitter(entry, MapRandomChannel.LineYaw, line.Index * 1000 + (int)along);
                        _placements.AddCopies(_layout.Lines, line.Index, entry, position, yaw, 1f, 1f);
                    }
                }
            }
        }

        private void PlaceScatters()
        {
            foreach (var scatter in _layout.Scatters.Where(s => s.Copy == 0))
            {
                var spec = scatter.Authored;
                var entry = _palette.Entry(spec.kind);
                var polygon = MapGeometry.ToVectors(spec.points);
                var bounds = MapGeometry.Bounds(polygon);
                var random = MapRandom.Stream(_layout.Spec.seed, MapRandomChannel.Scatters, scatter.Index);
                var placed = 0;
                for (var attempt = 0; attempt < spec.count * ScatterAttempts && placed < spec.count; attempt++)
                {
                    var position = MapGeometry.RandomIn(bounds, random);
                    var scale = random.Range(entry.scale.x, entry.scale.y);
                    var yaw = random.Signed() * entry.yawJitter;
                    if (entry.besideRoad && !MoveToRoadside(ref position, ref yaw, entry.Radius * scale))
                    {
                        continue;
                    }

                    if (Fits(polygon, position, entry.Radius * scale))
                    {
                        _placements.AddCopies(_layout.Scatters, scatter.Index, entry, position, yaw, scale, 1f);
                        placed++;
                    }
                }

                if (placed < spec.count)
                {
                    _issues.Warn($"Scatter {scatter.Index + 1} fit {placed} of {spec.count} '{entry.key}'.");
                }
            }
        }

        private bool MoveToRoadside(ref Vector2 position, ref float yaw, float radius)
        {
            if (!_shapes.RoadsideSpot(position, radius + RoadsideSetback, out position, out var roadYaw))
            {
                return false;
            }

            yaw += roadYaw;
            return true;
        }

        private bool Fits(List<Vector2> polygon, Vector2 position, float radius)
        {
            if (!MapGeometry.Contains(polygon, position))
            {
                return false;
            }

            return Clearance.IsOffRoad(position, radius) && Clearance.IsFree(position, radius);
        }

        private float Jitter(MapPaletteEntry entry, MapRandomChannel channel, int index)
        {
            if (entry.yawJitter <= 0f)
            {
                return 0f;
            }

            return MapRandom.Stream(_layout.Spec.seed, channel, index).Signed() * entry.yawJitter;
        }
    }
}
