using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Air
{
    /// <summary>
    /// Landing pads on a building, one aircraft each. Aircraft built here take a free pad (production waits while
    /// none is free); others claim one with a Return to Base order. Aircraft dock, landed, on their pad.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Airfield")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class AirfieldAuthoring : AuthoringBehaviour
    {
        [Tooltip("Pad positions relative to the building; each holds one aircraft.")]
        public Vector3[] padOffsets =
        {
            new(-3f, 0f, 3f), new(3f, 0f, 3f), new(-3f, 0f, -3f), new(3f, 0f, -3f),
        };

        public class Baker : Baker<AirfieldAuthoring>
        {
            public override void Bake(AirfieldAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var pads = AirSetup.AddAirfield(ref writer);
                foreach (var offset in authoring.padOffsets)
                {
                    pads.Add(new LandingPad { Offset = offset });
                }
            }
        }
    }
}
