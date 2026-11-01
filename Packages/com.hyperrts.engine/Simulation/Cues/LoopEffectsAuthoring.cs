using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Rigs that stream while the entity is shown: exhaust, chimney smoke, a burning wreck.</summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Loop Effects")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class LoopEffectsAuthoring : AuthoringBehaviour
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Looping rig prefab.")]
            public GameObject rig;

            [Tooltip("Child transform it streams from; none streams from the root.")]
            public Transform socket;

            [Tooltip("Streams only while the health fraction is above this (a chimney stops once hit); 0 always.")]
            [Range(0f, 1f)]
            public float aboveHealth;

            [Tooltip("Seconds it streams after the entity is first shown, then fades out; 0 forever.")]
            [Min(0f)]
            public float duration;
        }

        [Tooltip("Rigs streaming from this entity; entries without a rig are skipped.")]
        public Entry[] loops = { };

        public static DynamicBuffer<LoopEffect> Add<TWriter>(ref TWriter writer)
            where TWriter : struct, IEntityWriter =>
            writer.AddBuffer<LoopEffect>();

        public static void AddLoop(DynamicBuffer<LoopEffect> loops, GameObject rig, float3 socket, float aboveHealth,
            float duration)
        {
            if (rig != null)
            {
                loops.Add(new LoopEffect { Rig = rig, Socket = socket, AboveHealth = aboveHealth, Duration = duration });
            }
        }

        public class Baker : Baker<LoopEffectsAuthoring>
        {
            public override void Bake(LoopEffectsAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var loops = Add(ref writer);
                foreach (var entry in authoring.loops)
                {
                    var socket = Socket(this, authoring.transform, entry.socket);
                    AddLoop(loops, entry.rig, socket, entry.aboveHealth, entry.duration);
                }
            }
        }
    }
}
