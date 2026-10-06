using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Scales incoming damage per <see cref="DamageType"/> (e.g. tanks take 25% from bullets) and by the side the hit
    /// comes from (thin rear armor).
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Armor")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class ArmorAuthoring : AuthoringBehaviour
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

        [Header("Directional")]
        [Tooltip("Damage multiplier for hits within 45° of the front.")]
        [Min(0f)]
        public float front = 1f;

        [Tooltip("Damage multiplier for hits from the sides.")]
        [Min(0f)]
        public float side = 1f;

        [Tooltip("Damage multiplier for hits within 45° of the rear.")]
        [Min(0f)]
        public float rear = 1f;

        public bool IsDirectional => front != 1f || side != 1f || rear != 1f;

        public class Baker : Baker<ArmorAuthoring>
        {
            public override void Bake(ArmorAuthoring authoring)
            {
                var modifiers = new List<ArmorModifier>();
                foreach (var entry in authoring.modifiers)
                {
                    if (entry.damageType != null)
                    {
                        modifiers.Add(new ArmorModifier { DamageType = entry.damageType, Multiplier = entry.multiplier });
                    }
                }

                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                ArmorSetup.Add(ref sink, modifiers);
                if (authoring.IsDirectional)
                {
                    ArmorSetup.AddFacing(ref sink,
                        new ArmorFacing { Front = authoring.front, Side = authoring.side, Rear = authoring.rear });
                }
            }
        }
    }
}
