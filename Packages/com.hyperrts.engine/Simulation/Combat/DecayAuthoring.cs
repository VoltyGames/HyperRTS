using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Clears remains (a wreck) after a while: they sink into the ground, fading their loop effects, then the
    /// authoritative world destroys them. Put it on a ghost so clients follow.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Decay")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    public class DecayAuthoring : AuthoringBehaviour
    {
        [Tooltip("Seconds the remains stay before they start to sink.")]
        [Min(0.01f)]
        public float lifetime = 180f;

        [Tooltip("Seconds they take to sink out of sight; their loop effects fade over the same time.")]
        [Min(0.01f)]
        public float sinkDuration = 6f;

        [Tooltip("Metres they sink: at least their height above the ground.")]
        [Min(0.01f)]
        public float sinkDepth = 3f;

        public class Baker : Baker<DecayAuthoring>
        {
            public override void Bake(DecayAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                DecaySetup.Add(ref writer, authoring.name, authoring.lifetime, authoring.sinkDuration,
                    authoring.sinkDepth);
            }
        }
    }
}
