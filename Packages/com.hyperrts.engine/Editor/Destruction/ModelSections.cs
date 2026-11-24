using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Combat.Weapons;
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

            [Tooltip("The sections its broken pieces and heap lie on, by name.")]
            public string[] rests;

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

        [Serializable]
        public struct MovingPart
        {
            public string node;

            [Tooltip("The section it hides and shows with, by name.")]
            public string section;
        }

        [Tooltip("By mask bit.")]
        public List<Part> sections = new();

        public List<Hole> holes = new();

        public List<MovingPart> parts = new();

        /// <summary>The sections inside the model at <paramref name="path"/>, or null.</summary>
        public static ModelSections Find(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<ModelSections>().FirstOrDefault();

        /// <summary>Adds what one FBX node's properties say: a section, a moving part, a hole or nothing.</summary>
        public void Read(GameObject node, FbxProperties properties)
        {
            // A moving part names its section the same way, but has no order.
            if (properties.TryString(SectionProperty, out var owner) && !properties.Has("order"))
            {
                parts.Add(new MovingPart { node = node.name, section = owner });
            }
            else if (properties.TryString(SectionProperty, out var section))
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
                    rests = properties.List("rests"),
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
                bool IsSection(string name) => sections.Any(other => other.name == name);
                Require(part.supports, IsSection, name => $"is held by '{name}', which no mesh names as its section");
                Require(part.rests, IsSection, name => $"rests on '{name}', which no mesh names as its section");
                Require(part.holed, name => holes.Any(hole => hole.model == name), name => $"names holed mesh '{name}', which has no '{HoleProperty}'");

                void Require(IEnumerable<string> names, Func<string, bool> exists, Func<string, string> problem)
                {
                    var missing = names.FirstOrDefault(name => !exists(name));
                    if (missing != null)
                    {
                        throw new InvalidDataException($"HyperRTS: {source} section '{part.name}' {problem(missing)}.");
                    }
                }
            }

            var lost = parts.Find(moving => sections.All(part => part.name != moving.section));
            if (lost.node != null)
            {
                throw new InvalidDataException($"HyperRTS: {source} part '{lost.node}' belongs to section '{lost.section}', which no mesh names as its section.");
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
                parts = parts.Where(moving => moving.section == part.name).Select(moving => FindModel(owner, moving.node)).ToArray(),
                weapons = Weapons(owner, part.name),
                supports = SectionModels(owner, part.supports),
                rests = SectionModels(owner, part.rests),
                pieces = Hidden(owner, part.pieces),
                stub = Hidden(owner, part.stub),
                heap = Hidden(owner, part.heap),
                holes = Holes(owner, part, intact),
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

        private GameObject[] SectionModels(SectionsAuthoring owner, string[] names) =>
            names.Select(name => FindModel(owner, sections.Find(other => other.name == name).intact)).ToArray();

        private SectionsAuthoring.Hole[] Holes(SectionsAuthoring owner, Part part, GameObject intact) =>
            part.holed.Select(name => new SectionsAuthoring.Hole
            {
                model = Hidden(owner, name),
                center = owner.transform.InverseTransformPoint(intact.transform.TransformPoint(holes.Find(hole => hole.model == name).center)),
            }).ToArray();

        // The mounts whose muzzle, turret or barrel sit under one of the section's moving parts.
        private int[] Weapons(SectionsAuthoring owner, string sectionName)
        {
            var carriers = parts.Where(moving => moving.section == sectionName).Select(moving => FindModel(owner, moving.node).transform).ToList();
            if (!owner.TryGetComponent<WeaponAuthoring>(out var weapons))
            {
                return Array.Empty<int>();
            }

            var mounts = new List<int>();
            for (var m = 0; m < weapons.WeaponCount; m++)
            {
                if (weapons.MountStats(m).AimPoints().Any(point => carriers.Any(point.IsChildOf)))
                {
                    mounts.Add(m);
                }
            }

            return mounts.ToArray();
        }

        private GameObject Hidden(SectionsAuthoring owner, string modelName)
        {
            var model = FindModel(owner, modelName);
            model.transform.localScale = Vector3.zero;
            return model;
        }

        // Only the nodes of this model count, so dressing or a base prefab's child with the same name can't be taken.
        private GameObject FindModel(SectionsAuthoring owner, string modelName)
        {
            var modelPath = AssetDatabase.GetAssetPath(this);
            GameObject found = null;
            foreach (var child in owner.GetComponentsInChildren<Transform>(true))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
                if (child.name != modelName || source == null || AssetDatabase.GetAssetPath(source) != modelPath)
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
