using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// A building's <c>&lt;Asset&gt;.structure.json</c> from the structure tool: each section's box, material, floor,
    /// supports, model names and break clip rows. Only the keys listed here are read.
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

            public bool Same(Rows other) => startRow == other.startRow && frameCount == other.frameCount;
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
            public string material;
            public int floor;
            public string[] supports;
            public bool anchored;
            public bool exterior;
            public Clips clips;
            public Meshes meshes;
        }

        public string asset;
        public float fps;
        public Part[] sections;

        public static SectionStructure Parse(string json, string source)
        {
            var structure = JsonUtility.FromJson<SectionStructure>(json);
            if (structure.sections == null || structure.sections.Length == 0 || structure.fps <= 0f)
            {
                throw new InvalidOperationException($"HyperRTS: structure file '{source}' needs 'fps' and 'sections'.");
            }

            return structure;
        }

        /// <summary>The break clips, which every section of the building shares; throws when two sections differ.</summary>
        public SectionClips BreakClips(string source)
        {
            var first = sections[0].clips;
            foreach (var part in sections)
            {
                var same = part.clips.@in.Same(first.@in) && part.clips.@out.Same(first.@out) &&
                           part.clips.collapse.Same(first.collapse);
                if (!same)
                {
                    throw new InvalidOperationException($"HyperRTS: structure file '{source}' section '{part.name}' has its own clip rows.");
                }
            }

            return new SectionClips
            {
                BlownIn = first.@in.ToClip(fps), BlownOut = first.@out.ToClip(fps), Collapse = first.collapse.ToClip(fps),
            };
        }

        /// <summary>
        /// The sections as authoring entries under <paramref name="owner"/>: each model is the object of that exact name,
        /// and material (through the owner's name map), floor, anchored, exterior and supports come from the file. The
        /// share is guessed as Fill From Parts does. Holed, pieces, stub and heap models are set to scale 0.
        /// </summary>
        public List<SectionsAuthoring.Entry> Entries(SectionsAuthoring owner)
        {
            var found = new List<SectionsAuthoring.Entry>(sections.Length);
            foreach (var part in sections)
            {
                found.Add(Entry(owner, part));
            }

            var guessed = new SectionShape(owner).Guess(found);
            for (var i = 0; i < guessed.Count; i++)
            {
                guessed[i] = Corrected(owner, guessed[i], sections[i]);
            }

            return guessed;
        }

        private SectionsAuthoring.Entry Entry(SectionsAuthoring owner, Part part)
        {
            var intact = FindModel(owner, part.meshes.intact);
            var kept = Array.Find(owner.sections, item => item.intact == intact);
            var entry = new SectionsAuthoring.Entry
            {
                intact = intact,
                deck = kept.deck,
                needsAllSupports = kept.needsAllSupports,
                material = FindMaterial(owner, part.material),
                pieces = Hidden(owner, part.meshes.pieces),
                stub = Hidden(owner, part.meshes.stub),
                heap = Hidden(owner, part.meshes.heap),
                holes = new SectionsAuthoring.Hole[part.meshes.holed?.Length ?? 0],
            };
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

        // The file knows the walls and supports better than the box guess does.
        private SectionsAuthoring.Entry Corrected(SectionsAuthoring owner, SectionsAuthoring.Entry entry, Part part)
        {
            entry.floor = part.floor;
            entry.anchored = part.anchored;
            entry.exterior = part.exterior;
            var supports = new GameObject[part.supports.Length];
            for (var i = 0; i < supports.Length; i++)
            {
                var support = Array.Find(sections, other => other.name == part.supports[i]);
                supports[i] = FindModel(owner, support.meshes.intact);
            }

            entry.supports = supports;
            return entry;
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
