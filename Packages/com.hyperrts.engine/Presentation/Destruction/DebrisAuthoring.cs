using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>Throws client-side debris pieces when this ruin appears or when this building's parts break.</summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Debris")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class DebrisAuthoring : AuthoringBehaviour
    {
        [Tooltip("The pieces thrown.")]
        public DebrisSet set;

        [Tooltip("Metres from the centre the pieces start within.")]
        [Min(0f)]
        public float radius = 1f;

        [Tooltip("Throw once when it first appears, for ruins. Off throws from each breakable part as it breaks.")]
        public bool onAppear = true;

        public static void AddDebris<TWriter>(ref TWriter writer, DebrisSet set, float radius, bool onAppear)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Debris { Set = set, Radius = radius });
            writer.SetEnabled<Debris>(onAppear);
        }

        public class Baker : Baker<DebrisAuthoring>
        {
            public override void Bake(DebrisAuthoring authoring)
            {
                if (authoring.set == null)
                {
                    throw new InvalidOperationException($"Debris on '{authoring.name}' has no 'set'.");
                }

                var problem = authoring.set.Problem();
                if (problem != null)
                {
                    throw new InvalidOperationException(problem);
                }

                DependsOn(authoring.set);
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                AddDebris(ref writer, authoring.set, authoring.radius, authoring.onAppear);
            }
        }
    }
}
