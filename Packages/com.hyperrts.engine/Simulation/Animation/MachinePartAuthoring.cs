using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Animation
{
    /// <summary>A moving child of a Machine: a spinning crank, a rocking beam, a sliding crane or a rod.</summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "Machine Part")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Animation)]
    [DisallowMultipleComponent]
    public class MachinePartAuthoring : AuthoringBehaviour
    {
        [Tooltip("Spin about the axis, Swing to and fro about it, Slide along it, or Link one point to another.")]
        public MachineMotion motion;

        [Tooltip("Local axis for Spin and Swing, or the direction for Slide.")]
        public Vector3 axis = Vector3.right;

        [Tooltip("Degrees per second of the machine's cycle; give every part of one machine the same speed.")]
        public float speed = 45f;

        [Tooltip("Swing: largest angle either way of the offset (degrees). Slide: the same in metres.")]
        public float amplitude = 15f;

        [Tooltip("Swing: the angle it rocks about, in degrees from its modelled pose. Slide: metres along the axis.")]
        public float offset;

        [Tooltip("Degrees added to the cycle, to time this part against the others.")]
        public float phase;

        [Tooltip("Link: the transform this part sits on (a crank pin).")]
        public Transform follow;

        [Tooltip("Link: the transform this part's up axis points at (an equalizer bearing).")]
        public Transform aim;

        public class Baker : Baker<MachinePartAuthoring>
        {
            public override void Bake(MachinePartAuthoring authoring)
            {
                var machine = GetComponentInParent<MachineAuthoring>();
                if (machine == null)
                {
                    return;
                }

                var link = authoring.motion == MachineMotion.Link;
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new MachinePart
                {
                    Machine = GetEntity(machine, TransformUsageFlags.Dynamic),
                    Motion = authoring.motion,
                    Axis = authoring.axis.normalized,
                    Speed = authoring.speed,
                    Amplitude = authoring.amplitude,
                    Offset = authoring.offset,
                    Phase = authoring.phase,
                    Follow = link && authoring.follow != null ? GetEntity(authoring.follow, TransformUsageFlags.Dynamic) : Entity.Null,
                    Aim = link && authoring.aim != null ? GetEntity(authoring.aim, TransformUsageFlags.Dynamic) : Entity.Null,
                    Rest = GetComponent<Transform>().localRotation,
                    RestPosition = GetComponent<Transform>().localPosition,
                });
            }
        }
    }
}
