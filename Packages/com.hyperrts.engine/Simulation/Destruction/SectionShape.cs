using HyperRTS.Simulation.Common;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>The baked <see cref="Section"/>s of a <see cref="SectionsAuthoring"/>'s entries.</summary>
    public static class SectionShape
    {
        /// <summary>The sections, each with its share of <paramref name="maxHealth"/>; materials are listed in index order.</summary>
        public static List<Section> Bake(SectionsAuthoring.Entry[] entries, float maxHealth, List<SectionMaterial> materials)
        {
            var total = 0f;
            foreach (var entry in entries)
            {
                total += entry.share;
            }

            var bridge = Array.Exists(entries, entry => entry.deck);
            var sections = new List<Section>(entries.Length);
            foreach (var entry in entries)
            {
                var health = maxHealth * entry.share / math.max(total, 1e-4f);
                sections.Add(new Section
                {
                    Center = entry.center,
                    HalfSize = entry.halfSize,
                    Health = health,
                    MaxHealth = health,
                    Material = (byte)MaterialIndex(materials, entry.material),
                    Floor = (byte)entry.floor,
                    Supports = SupportBits(entries, entry.supports),
                    Anchored = entry.anchored,
                    Closes = entry.anchored && !bridge,
                    Exterior = entry.exterior,
                    Deck = entry.deck,
                    NeedsAllSupports = entry.needsAllSupports,
                    Outward = entry.outward,
                });
            }

            return sections;
        }

        private static ulong SupportBits(SectionsAuthoring.Entry[] entries, GameObject[] supports)
        {
            var bits = 0ul;
            foreach (var support in supports ?? Array.Empty<GameObject>())
            {
                var index = Array.FindIndex(entries, entry => entry.intact == support);
                if (index >= 0)
                {
                    bits |= 1ul << index;
                }
            }

            return bits;
        }

        private static int MaterialIndex(List<SectionMaterial> materials, SectionMaterial material)
        {
            var index = materials.IndexOf(material);
            if (index < 0)
            {
                materials.Add(material);
                index = materials.Count - 1;
            }

            return index;
        }
    }
}
