using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Air
{
    /// <summary>One landing pad of an airfield and the aircraft that calls it home.</summary>
    [InternalBufferCapacity(4)]
    public struct LandingPad : IBufferElementData
    {
        public float3 Offset;

        public PadKind Kind;

        /// <summary>The aircraft based here; <c>Entity.Null</c> while the pad is free.</summary>
        public Entity Aircraft;

        /// <summary>An Any pad takes every aircraft, and an Any aircraft lands on every pad.</summary>
        public readonly bool Takes(PadKind aircraft) => Kind == PadKind.Any || aircraft == PadKind.Any || Kind == aircraft;

        /// <summary>Index of the first free pad that takes <paramref name="aircraft"/>, or -1.</summary>
        public static int FindFree(DynamicBuffer<LandingPad> pads, PadKind aircraft)
        {
            for (var i = 0; i < pads.Length; i++)
            {
                if (pads[i].Aircraft == Entity.Null && pads[i].Takes(aircraft))
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

        /// <summary>Sent home once on running dry, so player orders hold until it has rounds again.</summary>
        public bool Recalled;

        /// <summary>The pads it lands on, kept when it changes or loses its home.</summary>
        public PadKind Kind;

        public readonly HomePad At(Entity airfield, int pad) => new() { Airfield = airfield, Pad = pad, Kind = Kind };

        public void Lose() => this = new HomePad { Kind = Kind };
    }

    /// <summary>Which aircraft a landing pad takes: helicopters on a helipad, jets in a shelter.</summary>
    public enum PadKind : byte
    {
        Any = 0,
        Helipad = 1,
        Shelter = 2,
    }

    /// <summary>
    /// Enabled while the aircraft sits landed on its pad: ammo reloads, and games refuel or repair it. Any move lifts
    /// it off again.
    /// </summary>
    [GhostEnabledBit]
    public struct Docked : IComponentData, IEnableableComponent { }
}
