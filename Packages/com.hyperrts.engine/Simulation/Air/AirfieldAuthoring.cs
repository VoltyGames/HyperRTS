using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
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
    public class AirfieldAuthoring : MonoBehaviour
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
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                var pads = AirSetup.AddAirfield(ref sink);
                foreach (var offset in authoring.padOffsets)
                {
                    pads.Add(new LandingPad { Offset = offset });
                }
            }
        }
    }

    /// <summary>One landing pad of an airfield and the aircraft that calls it home.</summary>
    [InternalBufferCapacity(4)]
    public struct LandingPad : IBufferElementData
    {
        public float3 Offset;

        /// <summary>The aircraft based here; <c>Entity.Null</c> while the pad is free.</summary>
        public Entity Aircraft;

        /// <summary>Index of the first free pad, or -1.</summary>
        public static int FindFree(DynamicBuffer<LandingPad> pads)
        {
            for (var i = 0; i < pads.Length; i++)
            {
                if (pads[i].Aircraft == Entity.Null)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
