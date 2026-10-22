using Unity.Entities;
using Unity.Netcode;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Who last damaged this entity; read on death for kill credit.</summary>
    public struct LastAttacker : IComponentData
    {
        public Entity Source;
        public byte Faction;
    }

    /// <summary>Prefab instantiated where the entity dies (wreck, debris, explosion).</summary>
    public struct SpawnOnDeath : IComponentData
    {
        public Entity Prefab;
    }

    /// <summary>Tag: this entity's remains fall to the ground instead of staying where it died (aircraft).</summary>
    public struct DropRemains : IComponentData
    {
    }

    /// <summary>Remains falling to the ground under gravity; removed when they land.</summary>
    public struct FallingRemains : IComponentData
    {
        /// <summary>m/s squared, shared by everything shown falling.</summary>
        public const float Gravity = 9.81f;

        public float Speed;

        /// <summary>Drops <paramref name="height"/> one step; true once it reaches <paramref name="ground"/>.</summary>
        public bool Fall(ref float height, float ground, float deltaTime)
        {
            Speed += Gravity * deltaTime;
            height -= Speed * deltaTime;
            if (height > ground)
            {
                return false;
            }

            height = ground;
            return true;
        }
    }

    /// <summary>Remains that last <see cref="Lifetime"/> seconds, then sink <see cref="DespawnDepth"/> metres and go.</summary>
    public struct Decay : IComponentData
    {
        public float Lifetime;

        public float DespawnDuration;

        public float DespawnDepth;

        /// <summary>Elapsed time the despawn starts, set by the authoritative world on its first look; 0 until then.</summary>
        public double DespawnStartTime;

        /// <summary>Share of the despawn done, 0 to 1. Replicated so clients fade the remains' loop effects with it.</summary>
        [GhostField(Quantization = 100)] public float DespawnProgress;
    }

    /// <summary>
    /// An effect played once where the entity appears (the dust of a collapse on a ruin, a fallen tree). Enabled
    /// until presentation plays it, then disabled; an entity that shows a result nobody saw happen spawns disabled.
    /// </summary>
    public struct SpawnEffect : IComponentData, IEnableableComponent
    {
        public UnityObjectRef<GameObject> Prefab;
    }
}
