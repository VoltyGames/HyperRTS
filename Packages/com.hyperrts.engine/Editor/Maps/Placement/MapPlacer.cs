using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement
{
    /// <summary>
    /// Places the spec's palette prefabs: start kits, resources, props, bridges, prop rows and scatters; the land
    /// planners add settlements and surface props through it. Random choices are made once in the authored sector
    /// and copied, so every copy matches.
    /// </summary>
    public sealed class MapPlacer
    {
        private const int ScatterAttempts = 40;
        private readonly MapLayout _layout;
        private readonly MapShapes _shapes;
        private readonly MapPalette _palette;
        private readonly MapClimate _climate;
        private readonly MapIssues _issues;

        private MapPlacer(MapLayout layout, MapShapes shapes, MapPalette palette, MapClimate climate, MapIssues issues)
        {
            _layout = layout;
            _shapes = shapes;
            _palette = palette;
            _climate = climate;
            _issues = issues;
            Clearance = new MapClearance(layout, shapes, palette.baseRadius);
        }

        public List<MapPlacement> Placements { get; } = new();

        public MapClearance Clearance { get; }

        public static MapPlacer Place(MapLayout layout, MapShapes shapes, MapPalette palette, MapClimate climate,
            MapIssues issues)
        {
            var placer = new MapPlacer(layout, shapes, palette, climate, issues);
            placer.PlaceStartKits();
            placer.PlaceResources();
            placer.PlaceProps();
            placer.PlaceBridges();
            placer.PlaceLines();
            placer.PlaceScatters();
            return placer;
        }

        /// <summary>Places an authored-space choice in every copy of the feature it belongs to.</summary>
        internal void AddCopies<T>(List<MapCopy<T>> copies, int index, MapPaletteEntry entry, Vector2 position,
            float yaw, float scale) where T : class
        {
            foreach (var copy in copies.Where(c => c.Index == index))
            {
                Add(new MapPlacement(entry, copy.Transform.Apply(position), copy.Transform.ApplyYaw(yaw)) { Scale = scale });
            }
        }

        /// <summary>Places a choice made at an authored-sector point in every copy of the sector.</summary>
        internal void AddEverywhere(MapPaletteEntry entry, Vector2 sectorPoint, float yaw, float scale)
        {
            foreach (var copy in _layout.Symmetry.Copies)
            {
                Add(new MapPlacement(entry, copy.Apply(sectorPoint), copy.ApplyYaw(yaw)) { Scale = scale });
            }
        }

        internal void Add(MapPlacement placement)
        {
            Placements.Add(placement);
            Clearance.Reserve(placement.Position, placement.Radius);
        }

        private void PlaceStartKits()
        {
            foreach (var spawn in _layout.Spawns)
            {
                var forward = MapGeometry.Forward(spawn.Yaw);
                var right = MapGeometry.Right(forward);
                foreach (var item in _palette.startKit)
                {
                    var entry = _palette.Entry(item.key);
                    var position = spawn.Position + right * item.offset.x + forward * item.offset.y;
                    Add(new MapPlacement(entry, position, spawn.Yaw + item.yaw) { Owner = spawn.Slot });
                }
            }
        }

        private void PlaceResources()
        {
            foreach (var resource in _layout.Resources)
            {
                var entry = _palette.Entry(resource.Placed.kind);
                var yaw = resource.Transform.ApplyYaw(Jitter(entry, MapRandomChannel.ResourceYaw, resource.Index));
                Add(new MapPlacement(entry, resource.Placed.pos.ToVector2(), yaw) { Amount = resource.Placed.amount });
            }
        }

        private void PlaceProps()
        {
            foreach (var prop in _layout.Props)
            {
                var entry = _palette.Entry(prop.Placed.kind);
                var yaw = prop.Transform.ApplyYaw(prop.Authored.yaw + Jitter(entry, MapRandomChannel.PropYaw, prop.Index));
                Add(new MapPlacement(entry, prop.Placed.pos.ToVector2(), yaw));
            }
        }

        private void PlaceBridges()
        {
            foreach (var crossing in _shapes.Crossings.Where(c => c.IsBridge))
            {
                Add(new MapPlacement(_palette.Entry(crossing.Prop), crossing.Position, crossing.Yaw)
                {
                    Height = crossing.Height,
                    Deck = new Vector2(crossing.Width, crossing.Span + 2f),
                });
            }
        }

        private void PlaceLines()
        {
            foreach (var line in _layout.Lines.Where(l => l.Copy == 0))
            {
                var kind = line.Authored.kind;
                if (_climate.HasTree(kind))
                {
                    continue;
                }

                var entry = _palette.Entry(kind);

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
                        AddCopies(_layout.Lines, line.Index, entry, position, yaw, 1f);
                    }
                }
            }
        }

        private void PlaceScatters()
        {
            foreach (var scatter in _layout.Scatters.Where(s => s.Copy == 0))
            {
                var entry = _palette.Entry(scatter.Authored.kind);

                var polygon = MapGeometry.ToVectors(scatter.Authored.points);
                var bounds = MapGeometry.Bounds(polygon);
                var random = MapRandom.Stream(_layout.Spec.seed, MapRandomChannel.Scatters, scatter.Index);
                var placed = 0;
                for (var attempt = 0; attempt < scatter.Authored.count * ScatterAttempts && placed < scatter.Authored.count; attempt++)
                {
                    var position = MapGeometry.RandomIn(bounds, random);
                    var scale = random.Range(entry.scale.x, entry.scale.y);
                    var radius = entry.Radius * scale;
                    var yaw = random.Yaw();
                    if (!MapGeometry.Contains(polygon, position) || !Clearance.IsOffRoad(position, radius)
                        || !Clearance.IsFree(position, radius))
                    {
                        continue;
                    }

                    AddCopies(_layout.Scatters, scatter.Index, entry, position, yaw, scale);
                    placed++;
                }

                if (placed < scatter.Authored.count)
                {
                    _issues.Warn($"Scatter {scatter.Index + 1} fit {placed} of {scatter.Authored.count} '{entry.key}'.");
                }
            }
        }

        private float Jitter(MapPaletteEntry entry, MapRandomChannel channel, int index)
        {
            if (entry.yawJitter <= 0f)
            {
                return 0f;
            }

            var random = MapRandom.Stream(_layout.Spec.seed, channel, index);
            return random.Signed() * entry.yawJitter;
        }
    }
}
