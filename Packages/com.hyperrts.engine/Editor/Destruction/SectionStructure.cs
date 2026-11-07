using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Destruction;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Destruction
{
    /// <summary>
    /// A building's <c>&lt;Asset&gt;.structure.json</c> from the structure tool: the break clip rows and each section's
    /// box, facing, material, floor, supports and model names. Only the keys listed here are read.
    /// </summary>
    [Serializable]
    public class SectionStructure
    {
        [Serializable]
        public class Rows
        {
            public int startRow;
            public int frameCount;

            public SectionClip ToClip(float fps) => new() { StartRow = startRow, FrameCount = frameCount, Fps = fps };
        }

        [Serializable]
        public class Clips
        {
            public Rows @in;
            public Rows @out;
            public Rows collapse;
        }

        [Serializable]
        public class Hole
        {
            public string mesh;
            public float[] center;
        }

        [Serializable]
        public class Meshes
        {
            public string intact;
            public Hole[] holed;
            public string pieces;
            public string stub;
            public string heap;
        }

        [Serializable]
        public class Part
        {
            public string name;
            public float[] center;
            public float[] halfSize;
            public string material;
            public int floor;
            public string[] supports;
            public bool anchored;
            public bool exterior;
            public float[] outward;
            public Meshes meshes;

            public void Check(string source)
            {
                var shaped = center?.Length == 3 && halfSize?.Length == 3 && supports != null && meshes != null;
                if (!shaped || string.IsNullOrEmpty(meshes.intact) || exterior != (outward != null && outward.Length == 3))
                {
                    throw new InvalidOperationException($"HyperRTS: structure file '{source}' section '{name}' needs 'center', 'halfSize', 'supports', 'meshes' and an 'outward' exactly when 'exterior'.");
                }
            }

            /// <summary>The box in the owner's space; the file states it in the space of the intact model.</summary>
            public Bounds Box(SectionsAuthoring owner, Transform model)
            {
                var middle = new Vector3(center[0], center[1], center[2]);
                var size = 2f * new Vector3(halfSize[0], halfSize[1], halfSize[2]);
                return SectionsFill.Moved(new Bounds(middle, size), owner.transform.worldToLocalMatrix * model.localToWorldMatrix);
            }

            /// <summary>The way it faces in the owner's space: the file's for a wall, else away from <paramref name="middle"/>.</summary>
            public Vector3 Outward(SectionsAuthoring owner, Transform model, Vector3 boxCenter, float3 middle)
            {
                if (!exterior)
                {
                    return SectionsFill.Outward(boxCenter, middle);
                }

                var facing = new Vector3(outward[0], outward[1], outward[2]);
                return owner.transform.InverseTransformDirection(model.TransformDirection(facing)).normalized;
            }
        }

        public float fps;
        public Clips clips;
        public Part[] sections;

        public static SectionStructure Parse(string json, string source)
        {
            var structure = JsonUtility.FromJson<SectionStructure>(json);
            if (structure.sections == null || structure.sections.Length == 0 || structure.fps <= 0f)
            {
                throw new InvalidOperationException($"HyperRTS: structure file '{source}' needs 'fps' and 'sections'.");
            }

            if (structure.clips?.@in == null || structure.clips.@out == null || structure.clips.collapse == null)
            {
                throw new InvalidOperationException($"HyperRTS: structure file '{source}' needs 'clips' with 'in', 'out' and 'collapse'.");
            }

            foreach (var part in structure.sections)
            {
                part.Check(source);
            }

            return structure;
        }

        /// <summary>The break clips, which every section of the building shares.</summary>
        public SectionClips BreakClips() => new()
        {
            BlownIn = clips.@in.ToClip(fps), BlownOut = clips.@out.ToClip(fps), Collapse = clips.collapse.ToClip(fps),
        };

        /// <summary>
        /// The sections as authoring entries under <paramref name="owner"/>: each model is the object of that exact name,
        /// and box, facing, material (through the owner's name map), floor, anchored, exterior and supports come from
        /// the file. Share, deck and needs-all-supports stay from the owner's entry for the same model, else the share
        /// is guessed as Fill From Parts does. Holed, pieces, stub and heap models are set to scale 0.
        /// </summary>
        public List<SectionsAuthoring.Entry> Entries(SectionsAuthoring owner)
        {
            var boxes = new Bounds[sections.Length];
            for (var i = 0; i < boxes.Length; i++)
            {
                boxes[i] = sections[i].Box(owner, FindModel(owner, sections[i].meshes.intact).transform);
            }

            var all = boxes[0];
            foreach (var box in boxes)
            {
                all.Encapsulate(box);
            }

            var found = new List<SectionsAuthoring.Entry>(sections.Length);
            for (var i = 0; i < sections.Length; i++)
            {
                found.Add(Entry(owner, sections[i], boxes[i], all.center));
            }

            return found;
        }

        private SectionsAuthoring.Entry Entry(SectionsAuthoring owner, Part part, Bounds box, float3 middle)
        {
            var intact = FindModel(owner, part.meshes.intact);
            var keptAt = Array.FindIndex(owner.sections, item => item.intact == intact);
            var entry = new SectionsAuthoring.Entry
            {
                intact = intact,
                center = box.center,
                halfSize = box.extents,
                outward = part.Outward(owner, intact.transform, box.center, middle),
                material = FindMaterial(owner, part.material),
                floor = part.floor,
                anchored = part.anchored,
                exterior = part.exterior,
                supports = Supports(owner, part),
                pieces = Hidden(owner, part.meshes.pieces),
                stub = Hidden(owner, part.meshes.stub),
                heap = Hidden(owner, part.meshes.heap),
                holes = new SectionsAuthoring.Hole[part.meshes.holed?.Length ?? 0],
            };
            if (keptAt >= 0)
            {
                var kept = owner.sections[keptAt];
                entry.share = kept.share;
                entry.deck = kept.deck;
                entry.needsAllSupports = kept.needsAllSupports;
            }
            else
            {
                entry.share = SectionsFill.Share(box.size);
            }

            for (var i = 0; i < entry.holes.Length; i++)
            {
                var hole = part.meshes.holed[i];
                var center = new Vector3(hole.center[0], hole.center[1], hole.center[2]);
                entry.holes[i] = new SectionsAuthoring.Hole
                {
                    model = Hidden(owner, hole.mesh),
                    center = owner.transform.InverseTransformPoint(intact.transform.TransformPoint(center)),
                };
            }

            return entry;
        }

        private GameObject[] Supports(SectionsAuthoring owner, Part part)
        {
            var supports = new GameObject[part.supports.Length];
            for (var i = 0; i < supports.Length; i++)
            {
                var support = Array.Find(sections, other => other.name == part.supports[i]);
                supports[i] = FindModel(owner, support.meshes.intact);
            }

            return supports;
        }

        private static GameObject Hidden(SectionsAuthoring owner, string modelName)
        {
            var model = FindModel(owner, modelName);
            model.transform.localScale = Vector3.zero;
            return model;
        }

        private static GameObject FindModel(SectionsAuthoring owner, string modelName)
        {
            GameObject found = null;
            foreach (var child in owner.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != modelName)
                {
                    continue;
                }

                if (found != null)
                {
                    throw new InvalidOperationException($"HyperRTS: '{owner.name}' has two objects named '{modelName}'.");
                }

                found = child.gameObject;
            }

            return found != null
                ? found
                : throw new InvalidOperationException($"HyperRTS: '{owner.name}' has no object named '{modelName}' from the structure file.");
        }

        private static SectionMaterial FindMaterial(SectionsAuthoring owner, string materialName)
        {
            foreach (var item in owner.materialNames)
            {
                if (item.name == materialName && item.material != null)
                {
                    return item.material;
                }
            }

            throw new InvalidOperationException($"HyperRTS: sections on '{owner.name}' have no material for the structure file's '{materialName}'.");
        }
    }
}
