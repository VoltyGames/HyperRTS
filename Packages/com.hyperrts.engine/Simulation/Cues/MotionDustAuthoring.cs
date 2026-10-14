using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Dust this vehicle raises as it moves, by the ground material under it and its speed.</summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Motion Dust")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class MotionDustAuthoring : AuthoringBehaviour
    {
        [Tooltip("Dust rigs per ground material (dry dust on sand, spray on mud, none on paved).")]
        public SurfaceEffects effects;

        [Tooltip("Child transform the dust rises from (behind the rear wheels or tracks); none is the root.")]
        public Transform socket;

        [Tooltip("Speed in m/s at which the dust streams at full rate.")]
        [Min(0.1f)]
        public float fullSpeed = 8f;

        public class Baker : Baker<MotionDustAuthoring>
        {
            public override void Bake(MotionDustAuthoring authoring)
            {
                DependsOn(authoring.effects);
                if (authoring.socket != null)
                {
                    DependsOn(authoring.socket);
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var socket = EffectSetup.Socket(authoring.transform, authoring.socket);
                EffectSetup.AddMotionDust(ref writer, authoring.effects, socket, authoring.fullSpeed);
            }
        }
    }
}
