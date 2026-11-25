using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
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
        internal const string SectionProperty = "section";

        /// <summary>The holed mesh's property: the hole's centre in the model's space.</summary>
        internal const string HoleProperty = "hole";

        [Serializable]
        public class Section
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
        public List<Section> sections = new();

        public List<Hole> holes = new();

        public List<MovingPart> parts = new();

        /// <summary>The sections inside the model at <paramref name="path"/>, or null.</summary>
        public static ModelSections Find(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<ModelSections>().FirstOrDefault();

        /// <summary>Adds what one FBX node's properties say: a section, a moving part, a hole or nothing.</summary>
        public void Read(GameObject node, FbxProperties properties)
        {
            // A moving part names its section the same way, but has no order.
            var named = properties.TryString(SectionProperty, out var section);
            if (named && !properties.Has("order"))
            {
                parts.Add(new MovingPart { node = node.name, section = section });
            }
            else if (named)
            {
                var exterior = properties.Has("outward");
                sections.Add(new Section
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
        public void Finish(string source)
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

            var lost = parts.FindIndex(moving => sections.All(part => part.name != moving.section));
            if (lost >= 0)
            {
                var moving = parts[lost];
                throw new InvalidDataException($"HyperRTS: {source} part '{moving.node}' belongs to section '{moving.section}', which no mesh names as its section.");
            }
        }
    }
}
