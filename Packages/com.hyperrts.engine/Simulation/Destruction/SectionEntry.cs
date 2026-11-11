using System;
using UnityEngine;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>One section of a <see cref="SectionsAuthoring"/> building: its models, material, box and supports.</summary>
    [Serializable]
    public struct SectionEntry
    {
        [Tooltip("Shown while the section stands; its bounds are the section's box.")]
        public GameObject intact;

        [Tooltip("Shown once it breaks when it has no pieces: the holed wall or roof with its rubble. Never inside the intact model.")]
        public GameObject broken;

        [Tooltip("Holed variants of the intact model, at most 3; the one whose hole is nearest a seen impact shows. Start hidden at scale 0.")]
        public SectionsAuthoring.Hole[] holes;

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

        [Tooltip("Needs every support standing, not one (a bridge span): it falls when any of them goes.")]
        public bool needsAllSupports;

        [Tooltip("Centre of the section's box, in the building's space.")]
        public Vector3 center;

        [Tooltip("Half the section's box size, in the building's space.")]
        public Vector3 halfSize;

        [Tooltip("The way debris flies and which side a hit comes from: horizontal out of a wall, or up for a roof.")]
        public Vector3 outward;

        [Tooltip("Share of the building's health, against the others' shares.")]
        [Min(0.01f)]
        public float share;

        [Tooltip("Sections it rests on, by their intact model; it falls once none of them reach the ground.")]
        public GameObject[] supports;

        /// <summary>What stops this section baking on <paramref name="owner"/>; null when it is fine.</summary>
        public string Problem(SectionsAuthoring owner, int index)
        {
            if (intact == null)
            {
                return $"Section {index} on '{owner.name}' needs an intact model.";
            }

            var swaps = broken != null;
            var breaks = pieces != null && stub != null && heap != null;
            if (swaps == breaks)
            {
                return $"Section '{intact.name}' on '{owner.name}' needs either a broken model, or pieces with a stub and a heap.";
            }

            if (pieces != null && !owner.clips.IsSet)
            {
                return $"Section '{intact.name}' on '{owner.name}' has pieces but the building has no break clips; Fill From Model sets them.";
            }

            if (halfSize.x <= 0f || halfSize.y <= 0f || halfSize.z <= 0f)
            {
                return $"Section '{intact.name}' on '{owner.name}' has no box; run Fill From Model or Fill From Parts.";
            }

            if (material == null)
            {
                return $"Section '{intact.name}' on '{owner.name}' has no material.";
            }

            if ((holes?.Length ?? 0) > SectionModel.MostHoles)
            {
                return $"Section '{intact.name}' on '{owner.name}' has more than {SectionModel.MostHoles} holed variants.";
            }

            return ModelProblem(owner);
        }

        // Hidden in the prefab so editor views and map captures show the standing building.
        private string ModelProblem(SectionsAuthoring owner)
        {
            var models = new System.Collections.Generic.List<GameObject> { intact, broken, pieces, stub, heap };
            foreach (var hole in holes ?? Array.Empty<SectionsAuthoring.Hole>())
            {
                models.Add(hole.model);
            }

            foreach (var model in models)
            {
                if (model == null)
                {
                    continue;
                }

                if (!model.transform.IsChildOf(owner.transform))
                {
                    return $"Section '{intact.name}' on '{owner.name}' uses the model '{model.name}' from outside the building.";
                }

                if (model != intact && model.transform.IsChildOf(intact.transform))
                {
                    return $"Model '{model.name}' on '{owner.name}' sits inside '{intact.name}' and would hide with it.";
                }

                if (model != intact && model.transform.localScale != Vector3.zero)
                {
                    return $"Model '{model.name}' on '{owner.name}' should start hidden at scale 0.";
                }
            }

            return null;
        }
    }
}
