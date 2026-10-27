using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Drive-through bays where vehicles stop to load or unload at a building or resource node. Units without a
    /// chassis walk to the edge instead.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Navigation + "Dock")]
    [Icon(HyperRTSIcons.Navigation)]
    [HelpURL(HyperRTSDocs.Docks)]
    [DisallowMultipleComponent]
    public class DockAuthoring : AuthoringBehaviour
    {
        public const float DefaultRunIn = 6f;

        /// <summary>Metres straight ahead of the stop a bay without an exit child leaves to.</summary>
        public const float DefaultExit = 12f;

        private static readonly Regex StopName = new(@"^Bay_\d+$");

        [Serializable]
        public struct Bay
        {
            [Tooltip("Where the vehicle's centre stops, its +Z the way it faces.")]
            public Transform stop;

            [Tooltip("Where the straight run out ends. Empty leaves 12 m straight ahead of the stop.")]
            public Transform exit;

            [Tooltip("Metres the vehicle drives straight before the stop.")]
            [Min(0f)]
            public float runIn;
        }

        [Tooltip("The bays. Fill Bays in the context menu fills it from Bay_NN children and their Bay_NN_Exit siblings.")]
        public Bay[] bays = Array.Empty<Bay>();

        /// <summary>Lists every <c>Bay_NN</c> child in name order with its <c>Bay_NN_Exit</c> sibling, keeping run-ins.</summary>
        [ContextMenu("Fill Bays")]
        public void FillBays()
        {
            var stops = GetComponentsInChildren<Transform>(true).Where(child => StopName.IsMatch(child.name))
                .OrderBy(child => child.name, StringComparer.Ordinal);
            var filled = new List<Bay>();
            foreach (var stop in stops)
            {
                var kept = bays.FirstOrDefault(bay => bay.stop == stop);
                filled.Add(new Bay
                {
                    stop = stop,
                    exit = stop.parent.Find(stop.name + "_Exit"),
                    runIn = kept.stop == stop ? kept.runIn : DefaultRunIn,
                });
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Fill Bays");
#endif
            bays = filled.ToArray();
        }

        /// <summary>Every bay lists a stop, so <see cref="Bays"/> can read them.</summary>
        public bool IsComplete => bays.Length > 0 && bays.All(bay => bay.stop != null);

        /// <summary>The bays in this transform's space, as they bake.</summary>
        public DockBay[] Bays()
        {
            var baked = new DockBay[bays.Length];
            for (var i = 0; i < bays.Length; i++)
            {
                var bay = bays[i];
                if (bay.stop == null)
                {
                    throw new InvalidOperationException($"Dock on '{name}': bay {i} has no stop.");
                }

                float3 stop = transform.InverseTransformPoint(bay.stop.position);
                float3 forward = transform.InverseTransformDirection(bay.stop.forward);
                var facing = math.normalizesafe(forward.xz);
                var exit = bay.exit != null
                    ? (float3)transform.InverseTransformPoint(bay.exit.position)
                    : stop + new float3(facing.x, 0f, facing.y) * DefaultExit;
                baked[i] = new DockBay { Stop = stop, Facing = facing, RunIn = bay.runIn, Exit = exit };
            }

            return baked;
        }

        public static void Add<TWriter>(ref TWriter writer, string owner, IReadOnlyList<DockBay> bays)
            where TWriter : struct, IEntityWriter
        {
            if (bays.Count == 0)
            {
                throw new ArgumentException($"Dock on '{owner}' lists no bays.");
            }

            for (var i = 0; i < bays.Count; i++)
            {
                if (math.all(bays[i].Facing == 0f))
                {
                    throw new ArgumentException($"Dock on '{owner}': bay {i} faces straight up or down.");
                }
            }

            writer.AddBuffer(bays);
        }

        public class Baker : Baker<DockAuthoring>
        {
            public override void Bake(DockAuthoring authoring)
            {
                foreach (var bay in authoring.bays)
                {
                    DependsOn(bay.stop);
                    DependsOn(bay.exit);
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                DockAuthoring.Add(ref writer, authoring.name, authoring.Bays());
            }
        }
    }
}
