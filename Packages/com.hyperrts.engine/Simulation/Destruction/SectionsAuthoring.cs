using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>
    /// Makes a building out of sections that break on their own: hits land where they hit, what loses its supports
    /// falls, broken ground-floor walls open paths and leave rubble, and the building's health is the sum of theirs.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Destruction/Sections")]
    [Icon(HyperRTSIcons.SectionsAuthoring)]
    [HelpURL(HyperRTSDocs.Destruction)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Building")]
    public class SectionsAuthoring : AuthoringBehaviour
    {
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
            [Tooltip("The material's name in the model's sections.")]
            public string name;
            public SectionMaterial material;
        }

        [Tooltip("Which section material each material name in the model's sections means.")]
        public MaterialName[] materialNames = Array.Empty<MaterialName>();

        [Tooltip("How the pieces of every section break: blown in, blown out and collapse. Fill From Model sets them from the clip table the pieces play.")]
        public SectionClips clips;

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

        [Tooltip("The sections, at most 64. Fill From Model lists them from the sections kept inside the building's model, Fill From Parts from X_Intact and X_Broken children.")]
        public SectionEntry[] sections = Array.Empty<SectionEntry>();

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

        public class Baker : Baker<SectionsAuthoring>
        {
            public override void Bake(SectionsAuthoring authoring)
            {
                var problems = authoring.Problems();
                if (problems.Count > 0)
                {
                    throw new InvalidOperationException("HyperRTS: " + problems[0]);
                }

                var building = GetComponent<GameEntityAuthoring>();
                var materials = new List<SectionMaterial>();
                var sections = SectionShape.Bake(authoring.sections, building.maxHealth, materials);
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
                var writer = this.Writer();
                SectionsSetup.Add(writer, sections, Factors(materials), rubble);
                if (authoring.repairable)
                {
                    SectionsSetup.AddRepairable(writer, authoring.repairSeconds);
                }

                writer.AddBuffer(models);
                writer.AddBuffer(looks);
                writer.Add(new ShownSections { Effect = authoring.effect, DustWave = authoring.dustWave });
                if (authoring.clips.IsSet)
                {
                    writer.Add(authoring.clips);
                }
            }

            private SectionModel Model(in SectionEntry entry)
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
