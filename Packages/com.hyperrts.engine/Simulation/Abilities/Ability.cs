using HyperRTS.Simulation.Combat;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// A cooldown ability on a unit, building or player entity (support power). Built-in effects are optional: a
    /// prefab spawned at the target and damage (negative heals) around it. Every use is published as an
    /// <see cref="AbilityActivation"/> for game systems.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct Ability : IBufferElementData
    {
        /// <summary>Hash of the name; commands and events refer to it.</summary>
        public int Id;

        public FixedString32Bytes Name;
        public UnityObjectRef<Texture2D> Icon;
        public AbilityTarget Target;
        public AbilityTargetFilter Filter;

        /// <summary>Edge-to-edge cast range; 0 is unlimited.</summary>
        public float Range;

        public float Cooldown;
        public float CooldownRemaining;

        /// <summary>Building type the owner must have completed; 0 for none.</summary>
        public int RequiredTypeId;

        public Entity SpawnPrefab;
        public float Damage;
        public float Radius;
        public UnityObjectRef<DamageType> DamageType;
        public bool FriendlyFire;

        public readonly bool IsReady => CooldownRemaining <= 0f;
    }
}
