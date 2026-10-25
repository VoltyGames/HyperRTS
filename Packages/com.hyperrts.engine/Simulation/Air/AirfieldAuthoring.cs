using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// Landing pads on a building, one aircraft each. Aircraft built here take a free pad of their kind (production
    /// waits while none is free); others claim one with a Return to Base order. Aircraft dock, landed, on their pad.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Airfield")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Air)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class AirfieldAuthoring : AuthoringBehaviour
    {
        [Tooltip("Pads relative to the building; each holds one aircraft of its kind.")]
        public Pad[] pads =
        {
            new() { offset = new Vector3(-3f, 0f, 3f) }, new() { offset = new Vector3(3f, 0f, 3f) },
            new() { offset = new Vector3(-3f, 0f, -3f) }, new() { offset = new Vector3(3f, 0f, -3f) },
        };

        /// <summary>One landing pad: where it is and which aircraft it takes.</summary>
        [Serializable]
        public struct Pad
        {
            [Tooltip("Position relative to the building.")]
            public Vector3 offset;

            [Tooltip("Helipad for helicopters, Shelter for jets, Any for both.")]
            public PadKind kind;
        }

        public class Baker : Baker<AirfieldAuthoring>
        {
            public override void Bake(AirfieldAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var pads = AirSetup.AddAirfield(ref writer);
                foreach (var pad in authoring.pads)
                {
                    pads.Add(new LandingPad { Offset = pad.offset, Kind = pad.kind });
                }
            }
        }
    }
}
