using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Makes a building out of sections that break on their own: hits land where they hit, what loses its supports
    /// falls, broken ground-floor walls open paths and leave rubble, and the building's health is the sum of theirs.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Sections")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Building")]
    public class SectionsAuthoring : AuthoringBehaviour
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Shown while the section stands; its bounds are the section's box.")]
            public GameObject intact;

            [Tooltip("Shown once it breaks: the holed wall or roof with its rubble. Never inside the intact model.")]
            public GameObject broken;

            [Tooltip("What it is made of, which scales each damage type.")]
            public SectionMaterial material;

            [Tooltip("Storey it belongs to, 0 for the ground floor. Passengers on a floor get out when a section of it falls.")]
            [Min(0)]
            public int floor;

            [Tooltip("Stands on the ground, so it holds up whatever rests on it.")]
            public bool anchored;

            [Tooltip("An outer wall, which shelters a garrison and leaves rubble when it breaks on the ground floor.")]
            public bool exterior;

            [Tooltip("Share of the building's health, against the others' shares.")]
            [Min(0.01f)]
            public float share;

            [Tooltip("Sections it rests on, by their intact model; it falls once none of them reach the ground.")]
            public GameObject[] supports;
        }

        [Tooltip("Heap left at the foot of a broken ground-floor outer wall: crushable scenery tanks flatten.")]
        public GameObject rubble;

        [Tooltip("Effect rig played at a section as it breaks, +Z pointing out of the building (debris, dust).")]
        public GameObject effect;

        [Tooltip("The sections, at most 64. Fill From Parts in the context menu lists them from X_Intact and X_Broken children.")]
        public Entry[] sections = Array.Empty<Entry>();

        /// <summary>
        /// Lists every <c>X_Intact</c> child with its <c>X_Broken</c> sibling (hidden at scale 0), and guesses what
        /// the inspector then lets you correct: anchored when it reaches the ground, exterior when it is a wall, a full
        /// share for a wall and half for the rest, and supports from the sections it touches below. Materials and floors
        /// already set stay.
        /// </summary>
        [ContextMenu("Fill From Parts")]
        public void FillFromParts()
        {
            Record(this);
            var found = new List<Entry>();
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.EndsWith("_Intact", StringComparison.Ordinal))
                {
                    found.Add(Kept(child.gameObject, child.parent.Find(child.name[..^"_Intact".Length] + "_Broken")));
                }
            }

            var shape = new SectionShape(this);
            sections = shape.Guess(found).ToArray();
        }

        /// <summary>What stops the sections baking; empty when they are fine.</summary>
        public List<string> Problems()
        {
            var problems = new List<string>();
            if (sections.Length is 0 or > 64)
            {
                problems.Add($"Sections on '{name}' list {sections.Length}; a building needs 1 to 64.");
            }

            for (var i = 0; i < sections.Length; i++)
            {
                var problem = SectionProblem(sections[i], i);
                if (problem != null)
                {
                    problems.Add(problem);
                }
            }

            return problems;
        }

        private Entry Kept(GameObject intact, Transform broken)
        {
            if (broken != null)
            {
                Record(broken);
                broken.localScale = Vector3.zero;
            }

            var entry = Array.Find(sections, item => item.intact == intact);
            entry.intact = intact;
            entry.broken = broken != null ? broken.gameObject : null;
            return entry;
        }

        private string SectionProblem(in Entry entry, int index)
        {
            if (entry.intact == null || entry.broken == null)
            {
                return $"Section {index} on '{name}' needs both an intact and a broken model.";
            }

            if (!entry.intact.transform.IsChildOf(transform) || !entry.broken.transform.IsChildOf(transform))
            {
                return $"Section {index} on '{name}' uses a model from outside the building.";
            }

            if (entry.broken.transform.IsChildOf(entry.intact.transform))
            {
                return $"Section '{entry.broken.name}' on '{name}' sits inside '{entry.intact.name}' and would hide with it.";
            }

            if (entry.material == null)
            {
                return $"Section '{entry.intact.name}' on '{name}' has no material.";
            }

            // Hidden in the prefab so editor views and map captures show the standing building.
            return entry.broken.transform.localScale == Vector3.zero
                ? null
                : $"Section '{entry.broken.name}' on '{name}' should start hidden at scale 0.";
        }

        private static void Record(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(target, "Fill From Parts");
#endif
        }

        public class Baker : Baker<SectionsAuthoring>
        {
            public override void Bake(SectionsAuthoring authoring)
            {
                var problems = authoring.Problems();
                if (problems.Count > 0)
                {
                    throw new InvalidOperationException("HyperRTS: " + problems[0]);
                }

                // Through the baker, so moves and mesh edits rebake.
                GetComponentsInChildren<Transform>();
                foreach (var filter in GetComponentsInChildren<MeshFilter>())
                {
                    DependsOn(filter.sharedMesh);
                }

                var building = GetComponent<GameEntityAuthoring>();
                var materials = new List<SectionMaterial>();
                var sections = new SectionShape(authoring).Bake(authoring.sections, building.maxHealth, materials);
                var models = new List<SectionModel>(sections.Count);
                foreach (var entry in authoring.sections)
                {
                    models.Add(new SectionModel
                    {
                        Intact = GetEntity(entry.intact, TransformUsageFlags.Dynamic),
                        Broken = GetEntity(entry.broken, TransformUsageFlags.Dynamic),
                    });
                }

                var rubble = authoring.rubble != null
                    ? GetEntity(authoring.rubble, TransformUsageFlags.Dynamic)
                    : Entity.Null;
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                SectionsSetup.Add(ref writer, sections, Factors(materials), rubble);
                writer.AddBuffer(models);
                writer.Add(new ShownSections { Effect = authoring.effect });
            }

            private List<SectionFactor> Factors(List<SectionMaterial> materials)
            {
                var factors = new List<SectionFactor>();
                for (var i = 0; i < materials.Count; i++)
                {
                    DependsOn(materials[i]);
                    foreach (var factor in materials[i].factors)
                    {
                        if (factor.damageType != null)
                        {
                            factors.Add(new SectionFactor
                            {
                                Material = (byte)i, DamageType = factor.damageType, Multiplier = factor.multiplier,
                            });
                        }
                    }
                }

                return factors;
            }
        }
    }
}
