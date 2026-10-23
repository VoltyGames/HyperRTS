using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Dresses a building as a construction site while it is built (fencing, crane, pallets, work lights), streams a
    /// work rig from the site's <c>Work_01</c> onward empties while construction moves, and plays a rig on completion.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Construction Site")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Buildings)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class ConstructionSiteAuthoring : AuthoringBehaviour
    {
        public const string WorkPrefix = "Work_";

        [Tooltip("Child holding the site dressing, shown only while under construction; none for no dressing.")]
        public GameObject site;

        [Tooltip("Looping rig streamed at the site's Work_ empties while construction is moving (dust, welding sparks).")]
        public GameObject workRig;

        [Tooltip("Rig played once at the building when its construction completes.")]
        public GameObject completeEffect;

        public class Baker : Baker<ConstructionSiteAuthoring>
        {
            public override void Bake(ConstructionSiteAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new ConstructionSite
                {
                    Visual = authoring.site == null ? Entity.Null : GetEntity(authoring.site, TransformUsageFlags.Dynamic),
                    WorkRig = authoring.workRig,
                    CompleteEffect = authoring.completeEffect,
                });
                writer.Add(new ConstructionWatch());
                var points = writer.AddBuffer<ConstructionWorkPoint>();
                if (authoring.site == null || authoring.workRig == null)
                {
                    return;
                }

                var transforms = new List<Transform>();
                GetComponentsInChildren(authoring.site, transforms);
                foreach (var point in transforms)
                {
                    if (point.name.StartsWith(WorkPrefix, StringComparison.Ordinal))
                    {
                        points.Add(new ConstructionWorkPoint { Socket = EffectSetup.Socket(this, authoring.transform, point) });
                    }
                }
            }
        }
    }
}
