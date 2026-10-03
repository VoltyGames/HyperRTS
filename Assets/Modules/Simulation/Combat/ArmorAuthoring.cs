using System;
using System.Collections.Generic;
using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Scales incoming damage per <see cref="DamageType"/> (e.g. tanks take 25% from bullets).</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Armor")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class ArmorAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Damage type this entry applies to.")]
            public DamageType damageType;

            [Tooltip("Damage multiplier: 0.25 = takes a quarter, 2 = takes double.")]
            [Min(0f)]
            public float multiplier;
        }

        [Tooltip("Per-type multipliers; unlisted types deal full damage.")]
        public List<Entry> modifiers = new();

        public class Baker : Baker<ArmorAuthoring>
        {
            public override void Bake(ArmorAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<ArmorModifier>(entity);
                foreach (var entry in authoring.modifiers)
                {
                    if (entry.damageType != null)
                    {
                        buffer.Add(new ArmorModifier { DamageType = entry.damageType, Multiplier = entry.multiplier });
                    }
                }
            }
        }
    }

    [InternalBufferCapacity(2)]
    public struct ArmorModifier : IBufferElementData
    {
        public UnityObjectRef<DamageType> DamageType;
        public float Multiplier;
    }
}
