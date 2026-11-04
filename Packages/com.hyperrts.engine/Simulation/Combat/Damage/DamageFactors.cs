using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Stats;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>Read-only view of what scales a hit on a target: armor by damage type and by side, and damage taken.</summary>
    public struct DamageFactors
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private ComponentLookup<ArmorFacing> _facing;
        [ReadOnly] private BufferLookup<ArmorModifier> _armor;
        [ReadOnly] private BufferLookup<StatModifier> _modifiers;
        [ReadOnly] private ComponentLookup<Pace> _paces;

        public DamageFactors(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _facing = state.GetComponentLookup<ArmorFacing>(true);
            _armor = state.GetBufferLookup<ArmorModifier>(true);
            _modifiers = state.GetBufferLookup<StatModifier>(true);
            _paces = state.GetComponentLookup<Pace>(true);
        }

        public void Update(ref SystemState state)
        {
            _transforms.Update(ref state);
            _facing.Update(ref state);
            _armor.Update(ref state);
            _modifiers.Update(ref state);
            _paces.Update(ref state);
        }

        /// <summary>The target's armor multiplier against <paramref name="type"/>, 1 when unlisted.</summary>
        public float Armor(Entity target, UnityObjectRef<DamageType> type) => ArmorModifier.Against(_armor, target, type);

        /// <summary>Every multiplier the hit takes on the target: armor, the side it comes from, damage taken, then its pace.</summary>
        public float Scale(Entity target, in DamageEvent hit) =>
            Armor(target, hit.Type) * Facing(target, hit.Origin) * StatMath.DamageTaken(_modifiers, target, hit.Type)
            * (_paces.TryGetComponent(target, out var pace) ? pace.DamageTaken : 1f);

        private float Facing(Entity target, float3 origin) =>
            _facing.TryGetComponent(target, out var facing) && _transforms.TryGetComponent(target, out var transform)
                ? facing.Multiplier(transform, origin)
                : 1f;
    }
}
