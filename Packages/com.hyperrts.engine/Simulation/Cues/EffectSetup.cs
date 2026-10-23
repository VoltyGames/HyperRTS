using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Adds an entity's effect rigs. Unset rigs are skipped, so the entity writes no events for them.</summary>
    public static class EffectSetup
    {
        public static DynamicBuffer<EntityEffect> Add<TWriter>(ref TWriter writer)
            where TWriter : struct, IEntityWriter =>
            writer.AddBuffer<EntityEffect>();

        public static void Set(DynamicBuffer<EntityEffect> effects, CueSlot slot, GameObject rig, float3 socket,
            byte variant = 0)
        {
            if (rig != null)
            {
                effects.Add(new EntityEffect { Slot = slot, Variant = variant, Rig = rig, Socket = socket });
            }
        }

        public static void AddImpactEffects<TWriter>(ref TWriter writer, SurfaceEffects effects)
            where TWriter : struct, IEntityWriter
        {
            if (effects != null)
            {
                writer.Add(new EntityImpactEffects { Effects = effects });
            }
        }

        public static void AddHitMaterial<TWriter>(ref TWriter writer, SurfaceMaterial material)
            where TWriter : struct, IEntityWriter =>
            writer.Add(new HitMaterial { Value = material });

        public static DynamicBuffer<LoopEffect> AddLoops<TWriter>(ref TWriter writer)
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

        public static void AddTrail<TWriter>(ref TWriter writer, GameObject rig)
            where TWriter : struct, IEntityWriter
        {
            if (rig != null)
            {
                writer.Add(new EffectTrail { Rig = rig });
            }
        }

        public static void AddMotionDust<TWriter>(ref TWriter writer, SurfaceEffects effects, float3 socket,
            float minSpeed, float fullSpeed)
            where TWriter : struct, IEntityWriter
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

        /// <summary>A socket's position in <paramref name="root"/>'s local space; none is the origin. Moving it rebakes.</summary>
        public static float3 Socket(IBaker baker, Transform root, Transform socket)
        {
            if (socket == null)
            {
                return float3.zero;
            }

            baker.DependsOn(socket);
            return root.InverseTransformPoint(socket.position);
        }
    }
}
