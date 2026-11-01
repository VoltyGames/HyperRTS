using HyperRTS.Simulation.Combat.Damage;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// The fixed definition of a cooldown ability on a unit, building or player entity (support power). Built-in
    /// effects are optional: a prefab spawned at the target, damage (negative heals) around it and a status effect on
    /// an entity target. Every use is published as an <see cref="AbilityActivation"/> for game systems. It never
    /// changes and doesn't replicate: clients bake it from the same prefab, and the running cooldown is the
    /// replicated <see cref="AbilityState"/> with the same id.
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

        /// <summary>Building type the owner must have completed; 0 for none.</summary>
        public int RequiredTypeId;

        public Entity SpawnPrefab;
        /// <summary>Dealt round the target when its amount is set.</summary>
        public DamageProfile Damage;

        /// <summary>Status effect prefab applied to an entity target; <c>Entity.Null</c> for none.</summary>
        public Entity StatusEffect;
    }
}
