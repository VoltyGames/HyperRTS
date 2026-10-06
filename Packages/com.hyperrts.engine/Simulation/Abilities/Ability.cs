using HyperRTS.Simulation.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// A cooldown ability on a unit, building or player entity (support power). Built-in effects are optional: a
    /// prefab spawned at the target and damage (negative heals) around it. Every use is published as an
    /// <see cref="AbilityActivation"/> for game systems. Only the id and cooldown replicate and clients bake the rest,
    /// so the buffer must keep its baked length and order.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct Ability : IBufferElementData
    {
        /// <summary>Hash of the name; commands and events refer to it.</summary>
        [GhostField] public int Id;

        [GhostField(SendData = false)] public FixedString32Bytes Name;
        [GhostField(SendData = false)] public UnityObjectRef<Texture2D> Icon;
        [GhostField(SendData = false)] public AbilityTarget Target;
        [GhostField(SendData = false)] public AbilityTargetFilter Filter;

        /// <summary>Edge-to-edge cast range; 0 is unlimited.</summary>
        [GhostField(SendData = false)] public float Range;

        [GhostField(SendData = false)] public float Cooldown;
        /// <summary>Replicated to a tenth of a second, enough for the HUD.</summary>
        [GhostField(Quantization = 10)] public float CooldownRemaining;

        /// <summary>Building type the owner must have completed; 0 for none.</summary>
        [GhostField(SendData = false)] public int RequiredTypeId;

        [GhostField(SendData = false)] public Entity SpawnPrefab;
        [GhostField(SendData = false)] public float Damage;
        [GhostField(SendData = false)] public float Radius;
        [GhostField(SendData = false)] public UnityObjectRef<DamageType> DamageType;
        [GhostField(SendData = false)] public bool FriendlyFire;

        public readonly bool IsReady => CooldownRemaining <= 0f;
    }
}
