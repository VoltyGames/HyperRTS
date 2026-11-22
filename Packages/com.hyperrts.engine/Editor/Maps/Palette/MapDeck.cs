using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Navigation.Areas;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Palette
{
    /// <summary>A bridge's roadway as plain numbers, so planners on worker threads never touch its prefab.</summary>
    public readonly struct MapDeck
    {
        /// <summary>Width across by length along, from the prefab's Deck Nav Area.</summary>
        public readonly Vector2 Size;

        /// <summary>How far the building footprint runs over the approach slabs, past each end of the deck.</summary>
        public readonly float Landing;

        /// <summary>Length between the abutment faces, measured from the abutments its Nav Area lists (<see cref="NavAreaAuthoring.abutments"/>).</summary>
        public readonly float ClearSpan;

        private MapDeck(Vector2 size, float landing, float clearSpan)
        {
            Size = size;
            Landing = landing;
            ClearSpan = clearSpan;
        }

        /// <summary>Whether the abutments stand inside the deck, as the height shaper needs.</summary>
        public bool HasAbutments => ClearSpan > 0f && ClearSpan < Size.y;

        /// <summary>Reads a bridge prop's prefab on the main thread.</summary>
        public static MapDeck Read(MapProp entry)
        {
            var prefab = entry.prefab;
            var area = prefab.GetComponent<NavAreaAuthoring>();
            var size = area.size;
            var clearSpan = area.ClearSpan();
            if (!prefab.TryGetComponent<BuildingAuthoring>(out var building))
            {
                return new MapDeck(size, 0f, clearSpan);
            }

            return new MapDeck(size, Mathf.Max(0f, (building.footprint.y - size.y) * 0.5f), clearSpan);
        }
    }
}
