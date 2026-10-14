using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Simulation.Air
{
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

    /// <summary>The airfield and pad an aircraft returns to; <c>Entity.Null</c> while it has no home.</summary>
    public struct HomePad : IComponentData
    {
        public Entity Airfield;
        public int Pad;
    }

    /// <summary>
    /// Enabled while the aircraft sits landed on its pad: ammo reloads, and games refuel or repair it. Any move lifts
    /// it off again.
    /// </summary>
    [GhostEnabledBit]
    public struct Docked : IComponentData, IEnableableComponent { }
}
