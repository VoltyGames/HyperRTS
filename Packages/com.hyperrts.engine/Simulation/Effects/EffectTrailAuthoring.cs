using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>A rig streamed along this projectile's path (rocket smoke, a tracer glow).</summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Effect Trail")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class EffectTrailAuthoring : AuthoringBehaviour
    {
        [Tooltip("Rig that follows the projectile while it flies.")]
        public GameObject rig;

        public class Baker : Baker<EffectTrailAuthoring>
        {
            public override void Bake(EffectTrailAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                EffectSetup.AddTrail(ref writer, authoring.rig);
            }
        }
    }
}
