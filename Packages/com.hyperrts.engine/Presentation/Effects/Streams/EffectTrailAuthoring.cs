using HyperRTS.Core;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Effects.Streams
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

        public static void Add(EntityWriter writer, GameObject rig)
        {
            if (rig != null)
            {
                writer.Add(new EffectTrail { Rig = rig });
            }
        }

        public class Baker : Baker<EffectTrailAuthoring>
        {
            public override void Bake(EffectTrailAuthoring authoring)
            {
                var writer = this.Writer();
                Add(writer, authoring.rig);
            }
        }
    }
}
