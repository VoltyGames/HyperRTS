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
                FlagSetup.AddFlagMount(ref writer, GetEntity(authoring.flag, TransformUsageFlags.Dynamic),
                    flag.lossyScale.y, authoring.poleHeight);
            }
        }
    }
}
