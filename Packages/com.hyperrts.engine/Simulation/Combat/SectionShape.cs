using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Measures a <see cref="SectionsAuthoring"/>'s sections in the building's space: the boxes their intact models
    /// fill, the inspector guesses Fill From Parts makes from them, and the baked <see cref="Section"/>s.
    /// </summary>
    public sealed class SectionShape
    {
        /// <summary>Metres between boxes that still touch, and above the lowest box that still stands on the ground.</summary>
        private const float Contact = 0.05f;

        private readonly Transform _root;

        public SectionShape(SectionsAuthoring owner) => _root = owner.transform;

        /// <summary>
        /// Anchored, exterior, share and supports guessed from the boxes; the rest of each entry stays. Walls carry the
        /// building, so they take a full share and roofs and floors half.
        /// </summary>
        public List<SectionsAuthoring.Entry> Guess(List<SectionsAuthoring.Entry> entries)
        {
            var boxes = entries.ConvertAll(entry => Measure(entry.intact.transform));
            var ground = float.MaxValue;
            foreach (var box in boxes)
            {
                ground = math.min(ground, box.min.y);
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                entry.anchored = boxes[i].min.y <= ground + Contact;
                entry.exterior = IsWall(boxes[i].size);
                entry.share = entry.exterior ? 1f : 0.5f;
                entry.supports = Below(entries, boxes, i);
                entries[i] = entry;
            }

            return entries;
        }

        /// <summary>The sections, each with its share of <paramref name="maxHealth"/>; materials are listed in index order.</summary>
        public List<Section> Bake(SectionsAuthoring.Entry[] entries, float maxHealth, List<SectionMaterial> materials)
        {
            var total = 0f;
            foreach (var entry in entries)
            {
                total += entry.share;
            }

            var middle = Measure(_root).center;
            var sections = new List<Section>(entries.Length);
            foreach (var entry in entries)
            {
                var box = Measure(entry.intact.transform);
                var health = maxHealth * entry.share / math.max(total, 1e-4f);
                sections.Add(new Section
                {
                    Center = box.center,
                    HalfSize = box.extents,
                    Health = health,
                    MaxHealth = health,
                    Material = (byte)MaterialIndex(materials, entry.material),
                    Floor = (byte)entry.floor,
                    Supports = SupportBits(entries, entry.supports),
                    Anchored = entry.anchored,
                    Exterior = entry.exterior,
                    Deck = entry.deck,
                    NeedsAllSupports = entry.needsAllSupports,
                    Outward = Outward(box.center, middle),
                });
            }

            return sections;
        }

        /// <summary>Upright and thin, and no taller than twice its length, unlike a chimney.</summary>
        private static bool IsWall(Vector3 size)
        {
            var thin = math.min(size.x, size.z);
            var length = math.max(size.x, size.z);
            return thin < size.y && size.y <= 2f * length && thin <= 0.5f * length;
        }

        private static GameObject[] Below(List<SectionsAuthoring.Entry> entries, List<Bounds> boxes, int index)
        {
            var below = new List<GameObject>();
            var box = boxes[index];
            box.Expand(Contact * 2f);
            for (var i = 0; i < entries.Count; i++)
            {
                if (i != index && box.Intersects(boxes[i]) && boxes[i].center.y < boxes[index].center.y - Contact)
                {
                    below.Add(entries[i].intact);
                }
            }

            return below.ToArray();
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

        /// <summary>Out from the middle of the building, or up for a section over it.</summary>
        private static float3 Outward(float3 center, float3 middle)
        {
            var offset = center - middle;
            offset.y = 0f;
            return math.lengthsq(offset) > 1e-6f ? math.normalize(offset) : math.up();
        }

        /// <summary>The bounds of every mesh under <paramref name="node"/>, in the building's space.</summary>
        private Bounds Measure(Transform node)
        {
            var bounds = new Bounds();
            var any = false;
            foreach (var filter in node.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var toRoot = _root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var mesh = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f);
                    var point = toRoot.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents, sign));
                    if (any)
                    {
                        bounds.Encapsulate(point);
                    }
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                }
            }

            if (!any)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: sections on '{_root.name}': '{node.name}' has no mesh to measure.");
            }

            return bounds;
        }
    }
}
