using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Destruction;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Destruction
{
    /// <summary>
    /// A sectioned building's sections, read on import from its FBX properties and kept inside the model: each intact
    /// mesh's material, floor, supports, facing and other meshes, and each holed mesh's hole centre.
    /// </summary>
    public sealed class ModelSections : ScriptableObject
    {
        /// <summary>The intact mesh's property naming its section; the others sit beside it.</summary>
        public const string SectionProperty = "section";

        /// <summary>The holed mesh's property: the hole's centre in the model's space.</summary>
        public const string HoleProperty = "hole";

        [Serializable]
        public class Part
        {
            public string name;

            [Tooltip("The section's mask bit.")]
            public int order;

            public string intact;
            public string material;
            public int floor;
            public bool anchored;
            public bool exterior;

            [Tooltip("Which way an exterior section faces, in the model's space.")]
            public Vector3 outward;

            [Tooltip("The sections holding this one up, by name.")]
            public string[] supports;

            public string[] holed;
            public string pieces;
            public string stub;
            public string heap;
        }

        [Serializable]
        public struct Hole
        {
            public string model;

            [Tooltip("Where the hole is, in the model's space.")]
            public Vector3 center;
        }

        [Tooltip("By mask bit.")]
        public List<Part> sections = new();

        public List<Hole> holes = new();

        /// <summary>The sections inside the model at <paramref name="path"/>, or null.</summary>
        public static ModelSections Find(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<ModelSections>().FirstOrDefault();

        /// <summary>Adds what one FBX node's properties say: a section, a hole or nothing.</summary>
        public void Read(GameObject node, FbxProperties properties)
        {
            if (properties.TryString(SectionProperty, out var section))
            {
                var exterior = properties.Has("outward");
                sections.Add(new Part
                {
                    name = section,
                    order = properties.Int("order"),
                    intact = node.name,
                    material = properties.String("material"),
                    floor = properties.Int("floor"),
                    anchored = properties.Bool("anchored"),
                    exterior = exterior,
                    outward = exterior ? properties.Vector("outward") : Vector3.zero,
                    supports = properties.List("supports"),
                    holed = properties.List("holed"),
                    pieces = properties.String("pieces"),
                    stub = properties.String("stub"),
                    heap = properties.String("heap"),
                });
            }
            else if (properties.Has(HoleProperty))
            {
                holes.Add(new Hole { model = node.name, center = properties.Vector(HoleProperty) });
            }
        }

        /// <summary>Sorts the sections by mask bit and throws unless every support and holed mesh they name exists.</summary>
        public void Check(string source)
        {
            sections.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (var part in sections)
            {
                var support = part.supports.FirstOrDefault(name => sections.All(other => other.name != name));
                if (support != null)
                {
                    throw new InvalidDataException($"HyperRTS: {source} section '{part.name}' is held by '{support}', which no mesh names as its section.");
                }

                var holed = part.holed.FirstOrDefault(name => holes.All(hole => hole.model != name));
                if (holed != null)
                {
                    throw new InvalidDataException($"HyperRTS: {source} section '{part.name}' names holed mesh '{holed}', which has no '{HoleProperty}'.");
                }
            }
        }

        /// <summary>
        /// The sections as authoring entries under <paramref name="owner"/>, each box measured by
        /// <paramref name="measure"/>. Share, deck and needs-all-supports stay from the owner's entry for the same
        /// model. Holed, pieces, stub and heap models are set to scale 0.
        /// </summary>
        public List<SectionEntry> Entries(SectionsAuthoring owner, Func<Transform, Bounds> measure)
        {
            var intacts = sections.ConvertAll(part => FindModel(owner, part.intact));
            var boxes = intacts.ConvertAll(intact => measure(intact.transform));
            var all = boxes[0];
            foreach (var box in boxes)
            {
                all.Encapsulate(box);
            }

            var found = new List<SectionEntry>(sections.Count);
            for (var i = 0; i < sections.Count; i++)
            {
                found.Add(Entry(owner, sections[i], intacts[i], boxes[i], all.center));
            }

            return found;
        }

        private SectionEntry Entry(SectionsAuthoring owner, Part part, GameObject intact, Bounds box, Vector3 middle)
        {
            var keptAt = Array.FindIndex(owner.sections, item => item.intact == intact);
            var entry = new SectionEntry
            {
                intact = intact,
                center = box.center,
                halfSize = box.extents,
                outward = part.exterior
                    ? owner.transform.InverseTransformDirection(intact.transform.TransformDirection(part.outward)).normalized
                    : (Vector3)SectionsFill.Outward(box.center, middle),
                material = FindMaterial(owner, part.material),
                floor = part.floor,
                anchored = part.anchored,
                exterior = part.exterior,
                supports = part.supports.Select(name => FindModel(owner, sections.Find(other => other.name == name).intact)).ToArray(),
                pieces = Hidden(owner, part.pieces),
                stub = Hidden(owner, part.stub),
                heap = Hidden(owner, part.heap),
                holes = part.holed.Select(name => new SectionsAuthoring.Hole
                {
                    model = Hidden(owner, name),
                    center = owner.transform.InverseTransformPoint(intact.transform.TransformPoint(holes.Find(hole => hole.model == name).center)),
                }).ToArray(),
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

            return found ?? throw new InvalidOperationException($"HyperRTS: '{owner.name}' has no object named '{modelName}' from its model's sections.");
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

            throw new InvalidOperationException($"HyperRTS: sections on '{owner.name}' have no material for the model's '{materialName}'.");
        }
    }
}
