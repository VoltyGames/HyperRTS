using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Cues.Surfaces;
using System;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Effects.Streams
{
    /// <summary>Dust this rotorcraft's downwash kicks up from the ground under it as it flies low.</summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Rotor Wash")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class RotorWashAuthoring : AuthoringBehaviour
    {
        [Tooltip("Dust rigs per ground material (dry dust on soil, spray on water, none on paved).")]
        public SurfaceEffects effects;

        [Tooltip("Metres above the ground from which no dust rises; it thickens toward the ground.")]
        [Min(1f)]
        public float height = RotorWash.DefaultHeight;

        public static void AddRotorWash(EntityWriter writer, SurfaceEffects effects, float height) =>
            writer.Add(new RotorWash { Effects = effects, Height = height });

        public class Baker : Baker<RotorWashAuthoring>
        {
            public override void Bake(RotorWashAuthoring authoring)
            {
                if (authoring.effects == null)
                {
                    throw new InvalidOperationException($"Rotor Wash on '{authoring.name}' has no 'effects'.");
                }

                DependsOn(authoring.effects);
                var writer = this.Writer();
                AddRotorWash(writer, authoring.effects, authoring.height);
            }
        }
    }
}
