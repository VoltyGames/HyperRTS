using System;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    public partial class SectionsAuthoring
    {
        public partial struct Entry
        {
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

                if (pieces != null && owner.structure == null)
                {
                    return $"Section '{intact.name}' on '{owner.name}' has pieces but the building has no structure file for their clips.";
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
                foreach (var hole in holes ?? Array.Empty<Hole>())
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
}
