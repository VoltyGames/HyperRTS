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

        private MapDeck(Vector2 size, float landing)
        {
            Size = size;
            Landing = landing;
        }

        /// <summary>Reads a bridge prefab on the main thread.</summary>
        public static MapDeck Read(GameObject prefab)
        {
            var size = prefab.GetComponent<NavAreaAuthoring>().size;
            if (!prefab.TryGetComponent<BuildingAuthoring>(out var building))
            {
                return new MapDeck(size, 0f);
            }

            return new MapDeck(size, Mathf.Max(0f, (building.footprint.y - size.y) * 0.5f));
        }
    }
}
