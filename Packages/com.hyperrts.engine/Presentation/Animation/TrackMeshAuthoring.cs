using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Linq;
using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// A mesh of a track in a <see cref="MotionRigAuthoring"/>, which gets the properties the shader bends it with.
    /// Fill from model adds it to each mesh of a track.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "Track Mesh")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class TrackMeshAuthoring : AuthoringBehaviour
    {
        public class Baker : Baker<TrackMeshAuthoring>
        {
            public override void Bake(TrackMeshAuthoring authoring)
            {
                var rig = GetComponentInParent<MotionRigAuthoring>();
                var track = rig == null ? null : rig.tracks.FirstOrDefault(slot => slot.part == authoring.transform.parent);
                if (track == null)
                {
                    throw new InvalidOperationException($"Track Mesh '{authoring.name}' is not a mesh of a track in a Motion Rig.");
                }

                DependsOn(track.part);
                foreach (var wheel in track.wheels)
                {
                    DependsOn(wheel);
                }

                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new TrackSpan { Value = track.Span(rig, authoring.transform) });
                AddComponent<TrackLift0>(entity);
                AddComponent<TrackLift1>(entity);
            }
        }
    }
}
