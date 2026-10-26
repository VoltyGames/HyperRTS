using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Clears remains (a wreck) after a while: they are buried, fading their loop effects, then the
    /// authoritative world destroys them. Put it on a ghost so clients follow.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Decay")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    public class DecayAuthoring : AuthoringBehaviour
    {
        [Tooltip("Seconds the remains stay before they start to be buried.")]
        [Min(0.01f)]
        public float lifetime = 180f;

        [Tooltip("Seconds they take to be buried out of sight; their loop effects fade over the same time.")]
        [Min(0.01f)]
        public float despawnDuration = 6f;

        [Tooltip("Metres they are buried: at least their height above the ground.")]
        [Min(0.01f)]
        public float despawnDepth = 3f;

        public static void Add<TWriter>(ref TWriter writer, string owner, float lifetime, float despawnDuration,
            float despawnDepth) where TWriter : struct, IEntityWriter
        {
            Require(lifetime > 0f, owner, "lifetime");
            Require(despawnDuration > 0f, owner, "despawnDuration");
            Require(despawnDepth > 0f, owner, "despawnDepth");
            writer.Add(new Decay { Lifetime = lifetime, DespawnDuration = despawnDuration, DespawnDepth = despawnDepth });
        }

        private static void Require(bool positive, string owner, string field)
        {
            if (!positive)
            {
                throw new ArgumentException($"Decay on '{owner}': {field} must be above 0.");
            }
        }

        public class Baker : Baker<DecayAuthoring>
        {
            public override void Bake(DecayAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                DecayAuthoring.Add(ref writer, authoring.name, authoring.lifetime, authoring.despawnDuration,
                    authoring.despawnDepth);
            }
        }
    }
}
