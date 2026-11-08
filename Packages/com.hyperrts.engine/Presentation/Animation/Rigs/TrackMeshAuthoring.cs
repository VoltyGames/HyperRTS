using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>
    /// A track part of a <see cref="MotionRigAuthoring"/>, whose mesh gets the properties the shader bends it with.
    /// Fill from model adds it to each track.
    /// </summary>
    [AddComponentMenu("")]
    [Icon(HyperRTSIcons.TrackMeshAuthoring)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class TrackMeshAuthoring : AuthoringBehaviour
    {
        public class Baker : Baker<TrackMeshAuthoring>
        {
            public override void Bake(TrackMeshAuthoring authoring)
            {
                var rig = GetComponentInParent<MotionRigAuthoring>();
                var track = rig == null ? null : rig.tracks.FirstOrDefault(slot => slot.part == authoring.transform);
                if (track == null)
                {
                    throw new InvalidOperationException($"Track Mesh '{authoring.name}' is not a track part in a Motion Rig.");
                }

                DependsOn(track.part);
                foreach (var wheel in track.wheels)
                {
                    DependsOn(wheel);
                }

                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new TrackSpan { Value = track.Span(rig) });
                AddComponent<TrackLift0>(entity);
                AddComponent<TrackLift1>(entity);
            }
        }
    }
}
