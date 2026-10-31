using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Palette;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement
{
    /// <summary>
    /// Every palette prefab the map will instantiate, and the room they take: each placement reserves its footprint
    /// so later ones keep clear of it.
    /// </summary>
    public sealed class MapPlacements
    {
        private readonly MapLayout _layout;

        public MapPlacements(MapLayout layout, MapShapes shapes, float baseRadius)
        {
            _layout = layout;
            Clearance = new MapClearance(layout, shapes, baseRadius);
        }

        public List<MapPlacement> All { get; } = new();

        public MapClearance Clearance { get; }

        public void Add(MapPlacement placement)
        {
            All.Add(placement);
            Clearance.Reserve(placement.Position, placement.Radius);
        }

        /// <summary>
        /// Play-area props bake into the SubScene so the simulation sees them (sites clear them, tanks crush them), so a
        /// prefab without HyperRTS authoring is an error. It reads prefabs, so it runs on the main thread.
        /// </summary>
        public void CheckEntities(MapIssues issues)
        {
            foreach (var key in All.Where(p => p.Entry.prefab != null && !p.IsEntity).Select(p => p.Entry.key).Distinct())
            {
                issues.Error($"Prop '{key}' stands in the play area but its prefab has no HyperRTS authoring; " +
                             "make it a Destructible scenery prefab so the simulation sees it.");
            }
        }

        /// <summary>Places a choice made at an authored-sector point in every copy of the sector.</summary>
        public void AddEverywhere(MapProp entry, Vector2 sectorPoint, float yaw, float scale, float stretch)
        {
            foreach (var copy in _layout.Symmetry.Copies)
            {
                Add(new MapPlacement(entry, copy.Apply(sectorPoint), copy.ApplyYaw(yaw), stretch) { Scale = scale });
            }
        }

        /// <summary>Places an authored-space choice in every copy of the feature it belongs to.</summary>
        public void AddCopies<T>(List<MapCopy<T>> copies, int index, MapProp entry, Vector2 position,
            float yaw, float scale, float stretch) where T : class
        {
            foreach (var copy in copies.Where(c => c.Index == index))
            {
                Add(new MapPlacement(entry, copy.Transform.Apply(position), copy.Transform.ApplyYaw(yaw), stretch)
                {
                    Scale = scale,
                });
            }
        }
    }
}
