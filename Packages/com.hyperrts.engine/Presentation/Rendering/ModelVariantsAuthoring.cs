using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Shows one of several child models, picked by position, so repeated buildings such as walls vary.</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Model Variants")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class ModelVariantsAuthoring : AuthoringBehaviour
    {
        [Tooltip("The looks to pick from. Each lists every child that belongs to it, its damage stage models included.")]
        public List<Variant> variants = new();

        [Serializable]
        public class Variant
        {
            [Tooltip("Child objects shown only while this variant is picked.")]
            public List<GameObject> models = new();
        }

        public class Baker : Baker<ModelVariantsAuthoring>
        {
            public override void Bake(ModelVariantsAuthoring authoring)
            {
                var parts = new List<ModelVariantPart>();
                for (var i = 0; i < authoring.variants.Count; i++)
                {
                    foreach (var model in authoring.variants[i].models)
                    {
                        if (model == null)
                        {
                            continue;
                        }

                        var visual = GetEntity(model, TransformUsageFlags.Dynamic);
                        parts.Add(new ModelVariantPart { Visual = visual, Variant = i });
                    }
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.AddBuffer(parts);
                writer.Add(new ModelVariants { Count = authoring.variants.Count, Shown = -1 });
            }
        }
    }
}
