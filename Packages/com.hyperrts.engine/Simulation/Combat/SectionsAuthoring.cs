using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation;
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
    public partial class SectionsAuthoring : AuthoringBehaviour
    {
        [Serializable]
        public partial struct Entry
        {
            [Tooltip("Shown while the section stands; its bounds are the section's box.")]
            public GameObject intact;

            [Tooltip("Shown once it breaks when it has no pieces: the holed wall or roof with its rubble. Never inside the intact model.")]
            public GameObject broken;

            [Tooltip("Holed variants of the intact model, at most 3; the one whose hole is nearest a seen impact shows. Start hidden at scale 0.")]
            public Hole[] holes;

            [Tooltip("The pieces that fall apart when it breaks, a VAT rigid mesh with a VAT Animated component. Starts hidden at scale 0.")]
            public GameObject pieces;

            [Tooltip("What stays standing once it broke. Starts hidden at scale 0.")]
            public GameObject stub;

            [Tooltip("What lies at its foot once the pieces settled. Starts hidden at scale 0.")]
            public GameObject heap;

            [Tooltip("What it is made of, which scales each damage type.")]
            public SectionMaterial material;

            [Tooltip("Storey it belongs to, 0 for the ground floor. Passengers on a floor get out when a section of it falls.")]
            [Min(0)]
            public int floor;

            [Tooltip("Stands on the ground, so it holds up whatever rests on it.")]
            public bool anchored;

            [Tooltip("An outer wall, which shelters a garrison and leaves rubble when it breaks on the ground floor.")]
            public bool exterior;

            [Tooltip("Carries the walkable deck of this object's Nav Area (a bridge span): that stretch closes while it is broken.")]
            public bool deck;

            [Tooltip("Share of the building's health, against the others' shares.")]
            [Min(0.01f)]
            public float share;

            [Tooltip("Sections it rests on, by their intact model; it falls once none of them reach the ground.")]
            public GameObject[] supports;
        }

        [Serializable]
        public struct Hole
        {
            public GameObject model;

            [Tooltip("Where the hole is, in the building's space.")]
            public Vector3 center;
        }

        [Serializable]
        public struct MaterialName
        {
            [Tooltip("The material's name in the structure file.")]
            public string name;
            public SectionMaterial material;
        }

        [Tooltip("The building's <Asset>.structure.json from the structure tool. Fill From Structure lists the sections from it.")]
        public TextAsset structure;

        [Tooltip("Which section material each material name in the structure file means.")]
        public MaterialName[] materialNames = Array.Empty<MaterialName>();

        [Tooltip("Heap left at the foot of a broken ground-floor outer wall: crushable scenery tanks flatten.")]
        public GameObject rubble;

        [Tooltip("Effect rig played at a section as it breaks, +Z pointing out of the building (debris, dust).")]
        public GameObject effect;

        [Tooltip("Dust wave played at the foot of a ground floor section as it breaks.")]
        public GameObject dustWave;

        [Tooltip("Builders rebuild it for free, a section at a time, and it stays as a wreck instead of dying, like a bridge.")]
        public bool repairable;

        [Tooltip("Seconds a builder of rate 1 takes to rebuild one section.")]
        [Min(0.1f)]
        public float repairSeconds = 10f;

        [Tooltip("The sections, at most 64. Fill From Structure lists them from the structure file, Fill From Parts from X_Intact and X_Broken children.")]
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

        /// <summary>Lists the sections from the structure file; see <see cref="SectionStructure.Entries"/>.</summary>
        [ContextMenu("Fill From Structure")]
        public void FillFromStructure()
        {
            if (structure == null)
            {
                throw new InvalidOperationException($"HyperRTS: sections on '{name}' have no structure file to fill from.");
            }

            Record(this);
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                Record(child);
            }

            var file = SectionStructure.Parse(structure.text, structure.name);
            sections = file.Entries(this).ToArray();
        }

        /// <summary>What stops the sections baking; empty when they are fine.</summary>
        public List<string> Problems()
        {
            var problems = new List<string>();
            if (sections.Length is 0 or > 64)
            {
                problems.Add($"Sections on '{name}' list {sections.Length}; a building needs 1 to 64.");
            }

            if (Array.Exists(sections, section => section.deck) && !TryGetComponent<NavAreaAuthoring>(out _))
            {
                problems.Add($"Sections on '{name}' have a deck section but the object has no Nav Area.");
            }

            for (var i = 0; i < sections.Length; i++)
            {
                var problem = sections[i].Problem(this, i);
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

        private static void Record(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(target, "Fill sections");
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
                var looks = new List<SectionLook>(sections.Count);
                foreach (var entry in authoring.sections)
                {
                    models.Add(Model(entry));
                    looks.Add(SectionLook.Untouched);
                }

                var rubble = authoring.rubble != null
                    ? GetEntity(authoring.rubble, TransformUsageFlags.Dynamic)
                    : Entity.Null;
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                SectionsSetup.Add(ref writer, sections, Factors(materials), rubble);
                if (authoring.repairable)
                {
                    SectionsSetup.AddRepairable(ref writer, authoring.repairSeconds);
                }

                writer.AddBuffer(models);
                writer.AddBuffer(looks);
                writer.Add(new ShownSections { Effect = authoring.effect, DustWave = authoring.dustWave });
                if (authoring.structure != null)
                {
                    DependsOn(authoring.structure);
                    var file = SectionStructure.Parse(authoring.structure.text, authoring.structure.name);
                    writer.Add(file.BreakClips(authoring.structure.name));
                }
            }

            private SectionModel Model(in Entry entry)
            {
                var holes = entry.holes ?? Array.Empty<Hole>();
                return new SectionModel
                {
                    Intact = Find(entry.intact),
                    Broken = Find(entry.broken),
                    Pieces = Find(entry.pieces),
                    Stub = Find(entry.stub),
                    Heap = Find(entry.heap),
                    FirstHole = Find(holes, 0),
                    SecondHole = Find(holes, 1),
                    ThirdHole = Find(holes, 2),
                };
            }

            private SectionHole Find(Hole[] holes, int index) => index < holes.Length
                ? new SectionHole { Model = Find(holes[index].model), Center = holes[index].center }
                : default;

            private Entity Find(GameObject model) =>
                model != null ? GetEntity(model, TransformUsageFlags.Dynamic) : Entity.Null;

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
