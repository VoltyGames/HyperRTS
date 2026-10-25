using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Animation
{
    /// <summary>A moving child of a Machine: a spinning crank, a rocking beam, a sliding crane, a rod or a ram.</summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "Machine Part")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Animation)]
    [DisallowMultipleComponent]
    public class MachinePartAuthoring : AuthoringBehaviour
    {
        [Tooltip("Spin about the axis, Swing to and fro about it, Slide along it, Link one point to another, turn Downwind about it, Raise about it and Extend along it as the machine speeds up, or Point about it at the aim.")]
        public MachineMotion motion;

        [Tooltip("Local axis for Spin, Swing, Downwind, Raise and Point, or the direction for Slide and Extend.")]
        public Vector3 axis = Vector3.right;

        [Tooltip("Degrees per second of the machine's cycle; give every part of one machine the same speed.")]
        public float speed = 45f;

        [Tooltip("Swing: largest angle either way of the offset (degrees). Slide: the same in metres. Raise and Extend: degrees or metres at full speed.")]
        public float amplitude = 15f;

        [Tooltip("Swing: the angle it rocks about, in degrees from its modelled pose. Slide: metres along the axis. Downwind: degrees from its forward to the way it points.")]
        public float offset;

        [Tooltip("Degrees added to the cycle, to time this part against the others.")]
        public float phase;

        [Tooltip("Link: the transform this part sits on (a crank pin).")]
        public Transform follow;

        [Tooltip("Link: the transform this part's up axis points at (an equalizer bearing). Point: the pivot it keeps aiming at.")]
        public Transform aim;

        [Tooltip("Raise and Extend: the machine speed, 0 to 1, at which it starts moving; a deploying unit's jacks go before its launcher.")]
        [Range(0f, 1f)]
        public float from;

        [Tooltip("Raise and Extend: the machine speed at which it has finished; keep it above From.")]
        [Range(0f, 1f)]
        public float to = 1f;

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
                var aims = link || authoring.motion == MachineMotion.Point;
                var place = GetComponent<Transform>();
                DependsOn(authoring.aim);
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
                    Aim = aims && authoring.aim != null ? GetEntity(authoring.aim, TransformUsageFlags.Dynamic) : Entity.Null,
                    From = authoring.from,
                    To = authoring.to,
                    RestAim = authoring.aim != null ? place.parent.InverseTransformPoint(authoring.aim.position) : default,
                    Rest = place.localRotation,
                    RestPosition = place.localPosition,
                });
            }
        }
    }
}
