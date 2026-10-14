using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Flies the owner's flag over this building, lowering it and hoisting the capturer's as a capture runs.</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Flag Mount")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Buildings)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class FlagMountAuthoring : AuthoringBehaviour
    {
        [Tooltip("The flag's renderer at the pole top: origin at the top of the hoist, uniform scale = flag height (m).")]
        public Transform flag;

        [Tooltip("Pole length (m) from its foot up to the flag's origin.")]
        [Min(0f)]
        public float poleHeight = 6f;

        /// <summary>A fully lowered flag keeps one flag height of pole between it and the pole foot.</summary>
        public static void AddFlagMount<TWriter>(ref TWriter writer, Entity flag, float flagHeight,
            float poleHeight) where TWriter : struct, IEntityWriter
        {
            if (flagHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(flagHeight), flagHeight, "Flag height must be positive.");
            }

            var drop = poleHeight / flagHeight - 2f;
            if (drop < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(poleHeight), poleHeight,
                    $"A {poleHeight} m pole is too short for a {flagHeight} m flag (at least 2 flag heights).");
            }

            if (drop > FlagMount.MaxDrop)
            {
                throw new ArgumentOutOfRangeException(nameof(poleHeight), poleHeight,
                    $"A {poleHeight} m pole is too tall for a {flagHeight} m flag (at most {FlagMount.MaxDrop + 2f} flag heights).");
            }

            writer.Add(new FlagMount { Flag = flag, Drop = drop });
        }

        public class Baker : Baker<FlagMountAuthoring>
        {
            public override void Bake(FlagMountAuthoring authoring)
            {
                if (authoring.flag == null)
                {
                    throw new InvalidOperationException($"Flag Mount on '{authoring.name}' has no flag renderer.");
                }

                var flag = GetComponent<Transform>(authoring.flag);
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                FlagMountAuthoring.AddFlagMount(ref writer, GetEntity(authoring.flag, TransformUsageFlags.Dynamic),
                    flag.lossyScale.y, authoring.poleHeight);
            }
        }
    }
}
