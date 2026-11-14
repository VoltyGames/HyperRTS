using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Death;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Air
{
    /// <summary>Adds flight and pad use to aircraft and pads to airfields.</summary>
    public static class AirSetup
    {
        /// <summary>
        /// Flies the unit as <paramref name="flight"/> says; its <c>NavAgent</c> must be on the Air layer. Its remains
        /// fall to the ground. Drops leave from <paramref name="releasePoint"/>, in its local space.
        /// </summary>
        public static void AddFlight(EntityWriter writer, in Flight flight, float3 releasePoint = default)
        {
            writer.Add(flight);
            writer.Add<FlightState>();
            writer.Add(new DropRun { ReleasePoint = releasePoint });
            writer.Add<DropRemains>();
        }

        /// <summary>The aircraft takes a pad of <paramref name="kind"/> at the airfield that builds it and docks there to reload.</summary>
        public static void AddPadUser(EntityWriter writer, PadKind kind)
        {
            writer.Add(new HomePad { Kind = kind });
            writer.Add<Docked>();
            writer.SetEnabled<Docked>(false);
        }

        /// <summary>Fill the returned buffer with one <see cref="LandingPad"/> per pad.</summary>
        public static DynamicBuffer<LandingPad> AddAirfield(EntityWriter writer) =>
            writer.AddBuffer<LandingPad>();
    }
}
