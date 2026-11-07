using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Cues.Surfaces;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Effects.Streams
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

        [Tooltip("Below this speed in m/s a vehicle creeping or jostled in a crowd raises none.")]
        [Min(0f)]
        public float minSpeed = 1.5f;

        [Tooltip("Speed in m/s at which the dust streams at full rate.")]
        [Min(0.1f)]
        public float fullSpeed = 8f;

        public static void Add(EntityWriter writer, SurfaceEffects effects, float3 socket, float minSpeed,
            float fullSpeed)
        {
            if (effects != null)
            {
                writer.Add(new MotionDust
                {
                    Effects = effects,
                    Socket = socket,
                    MinSpeed = minSpeed,
                    FullSpeed = fullSpeed,
                });
            }
        }

        public class Baker : Baker<MotionDustAuthoring>
        {
            public override void Bake(MotionDustAuthoring authoring)
            {
                DependsOn(authoring.effects);
                var writer = this.Writer();
                var socket = Socket(this, authoring.transform, authoring.socket);
                Add(writer, authoring.effects, socket, authoring.minSpeed,
                    authoring.fullSpeed);
            }
        }
    }
}
