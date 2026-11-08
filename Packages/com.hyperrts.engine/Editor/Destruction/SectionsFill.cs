using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.Animation.VAT;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Destruction;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Destruction
{
    /// <summary>
    /// Fills a <see cref="SectionsAuthoring"/>'s sections from its parts or its model's sections, and measures the
    /// boxes their intact models fill in the building's space.
    /// </summary>
    public sealed class SectionsFill
    {
        /// <summary>Metres between boxes that still touch, and above the lowest box that still stands on the ground.</summary>
        private const float Contact = 0.05f;

        // The break clips every sectioned model's pieces play, named by its motion model.
        private const string BlownInClip = "In";
        private const string BlownOutClip = "Out";
        private const string CollapseClip = "Collapse";

        private readonly SectionsAuthoring _owner;
        private Transform Root => _owner.transform;

        public SectionsFill(SectionsAuthoring owner) => _owner = owner;

        [InspectorButton("Fill from parts", Glyph = "wand-sparkles")]
        private static void FillFromParts(SectionsAuthoring sections) => new SectionsFill(sections).FromParts();

        [InspectorButton("Fill from model", Glyph = "wand-sparkles")]
        private static void FillFromModel(SectionsAuthoring sections) => new SectionsFill(sections).FromModel();

        /// <summary>
        /// Lists every <c>X_Intact</c> child with its <c>X_Broken</c> sibling and guesses box, anchored, exterior,
        /// share and supports for you to correct. Materials and floors already set stay.
        /// </summary>
        public void FromParts()
        {
            var found = new List<SectionsAuthoring.Entry>();
            foreach (var child in _owner.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.EndsWith("_Intact", StringComparison.Ordinal))
                {
                    found.Add(Kept(child.gameObject, child.parent.Find(child.name[..^"_Intact".Length] + "_Broken")));
                }
            }

            _owner.sections = Guess(found).ToArray();
        }

        /// <summary>
        /// Lists the sections kept inside the building's model (see <see cref="ModelSections.Entries"/>) and takes the
        /// break clips from the clip table its pieces play.
        /// </summary>
        public void FromModel()
        {
            _owner.sections = FindSections().Entries(_owner, Measure).ToArray();
            _owner.clips = BreakClips();
        }

        // The one model under the building whose import kept sections.
        private ModelSections FindSections()
        {
            var found = _owner.GetComponentsInChildren<Transform>(true)
                .Select(child => PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject))
                .Where(source => source != null)
                .Select(AssetDatabase.GetAssetPath)
                .Distinct()
                .Select(ModelSections.Find)
                .Where(sections => sections != null)
                .ToList();
            return found.Count == 1
                ? found[0]
                : throw new InvalidOperationException($"HyperRTS: '{_owner.name}' holds {found.Count} models with sections; it needs one.");
        }

        private SectionClips BreakClips()
        {
            var tables = _owner.GetComponentsInChildren<VATAnimatedAuthoring>(true).Select(vat => vat.clips).Where(table => table != null).Distinct().ToList();
            if (tables.Count != 1)
            {
                throw new InvalidOperationException($"HyperRTS: '{_owner.name}' plays {tables.Count} VAT clip tables on its pieces; it needs one.");
            }

            return new SectionClips
            {
                BlownIn = BreakClip(tables[0], BlownInClip),
                BlownOut = BreakClip(tables[0], BlownOutClip),
                Collapse = BreakClip(tables[0], CollapseClip),
            };
        }

        private static SectionClip BreakClip(VATClipTable table, string clipName)
        {
            var range = table.Range(clipName);
            return new SectionClip { StartRow = range.StartRow, FrameCount = range.FrameCount, Fps = range.Fps };
        }

        /// <summary>
        /// Anchored, exterior, share and supports guessed from the boxes; the rest of each entry stays. Walls carry the
        /// building, so they take a full share and roofs and floors half.
        /// </summary>
        private List<SectionsAuthoring.Entry> Guess(List<SectionsAuthoring.Entry> entries)
        {
            var boxes = entries.ConvertAll(entry => Measure(entry.intact.transform));
            var middle = Measure(Root).center;
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
                entry.share = Share(boxes[i].size);
                entry.center = boxes[i].center;
                entry.halfSize = boxes[i].extents;
                entry.outward = Outward(boxes[i].center, middle);
                entry.supports = Below(entries, boxes, i);
                entries[i] = entry;
            }

            return entries;
        }

        private SectionsAuthoring.Entry Kept(GameObject intact, Transform broken)
        {
            if (broken != null)
            {
                broken.localScale = Vector3.zero;
            }

            var entry = Array.Find(_owner.sections, item => item.intact == intact);
            entry.intact = intact;
            entry.broken = broken != null ? broken.gameObject : null;
            return entry;
        }

        /// <summary>The box round <paramref name="box"/>'s corners moved by <paramref name="matrix"/>.</summary>
        public static Bounds Moved(Bounds box, Matrix4x4 matrix)
        {
            var moved = new Bounds(matrix.MultiplyPoint3x4(box.min), Vector3.zero);
            for (var corner = 1; corner < 8; corner++)
            {
                var sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                moved.Encapsulate(matrix.MultiplyPoint3x4(box.center + Vector3.Scale(box.extents, sign)));
            }

            return moved;
        }

        /// <summary>A full share for a wall and half for anything else.</summary>
        public static float Share(Vector3 size) => IsWall(size) ? 1f : 0.5f;

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

        /// <summary>Out from the middle of the building, or up for a section over it.</summary>
        public static float3 Outward(float3 center, float3 middle)
        {
            var offset = center - middle;
            offset.y = 0f;
            return math.lengthsq(offset) > 1e-6f ? math.normalize(offset) : math.up();
        }

        /// <summary>The bounds of every mesh under <paramref name="node"/>, in the building's space.</summary>
        public Bounds Measure(Transform node)
        {
            var bounds = new Bounds();
            var any = false;
            foreach (var filter in node.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var box = Moved(filter.sharedMesh.bounds, Root.worldToLocalMatrix * filter.transform.localToWorldMatrix);
                if (any)
                {
                    bounds.Encapsulate(box);
                }
                else
                {
                    bounds = box;
                    any = true;
                }
            }

            if (!any)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: sections on '{Root.name}': '{node.name}' has no mesh to measure.");
            }

            return bounds;
        }
    }
}
