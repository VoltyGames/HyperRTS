using HyperRTS.Editor.Maps.Data;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Placement
{
    /// <summary>One palette prefab to instantiate: where, which way, how big and for whom.</summary>
    public sealed class MapPlacement
    {
        public MapPlacement(MapPaletteEntry entry, Vector2 position, float yaw, float stretch)
        {
            Entry = entry;
            Position = position;
            Yaw = yaw;
            Stretch = stretch;
        }

        public MapPaletteEntry Entry { get; }

        public Vector2 Position { get; }

        public float Yaw { get; }

        public float Scale { get; set; } = 1f;

        /// <summary>Extra stretch along the entry's long side (an arch spanning its gap), on top of <see cref="Scale"/>.</summary>
        public float Stretch { get; }

        /// <summary>The long side is local Z when the entry's size is deeper than wide, else local X.</summary>
        private bool LongSideAlongZ => Entry.size.y >= Entry.size.x;

        /// <summary>Owning slot; 0 is neutral.</summary>
        public int Owner { get; set; }

        /// <summary>Resource amount override; 0 keeps the prefab's.</summary>
        public int Amount { get; set; }

        /// <summary>Set for prefabs that sit at a fixed height (bridge decks) instead of on the ground.</summary>
        public float? Height { get; set; }

        /// <summary>
        /// A walkable deck (width across, length along the prefab) the instance carries, so destroying it takes the
        /// crossing with it.
        /// </summary>
        public Vector2? Deck { get; set; }

        public float Radius
        {
            get
            {
                var size = Entry.size;
                var length = Mathf.Max(size.x, size.y) * Stretch;
                return Mathf.Max(length, Mathf.Min(size.x, size.y)) * 0.5f * Scale;
            }
        }

        /// <summary>The instance's local scale: uniform, with the long side stretched by <see cref="Stretch"/>.</summary>
        public Vector3 LocalScale => LongSideAlongZ
            ? new Vector3(Scale, Scale, Scale * Stretch)
            : new Vector3(Scale * Stretch, Scale, Scale);

        public bool IsEntity => Entry.IsEntity;
    }
}
