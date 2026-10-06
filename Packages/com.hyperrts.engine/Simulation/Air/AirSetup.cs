using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Air
{
    /// <summary>Adds flight and pad use to aircraft and pads to airfields.</summary>
    public static class AirSetup
    {
        /// <summary>Flies the unit at <paramref name="altitude"/>; its <c>NavAgent</c> must be on the Air layer.</summary>
        public static void AddFlight<TWriter>(ref TWriter writer, float altitude, float climbSpeed, float loiterRadius)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Flight { Altitude = altitude, ClimbSpeed = climbSpeed, LoiterRadius = loiterRadius });
        }

        /// <summary>The aircraft takes a pad at the airfield that builds it and docks there to rearm.</summary>
        public static void AddPadUser<TWriter>(ref TWriter writer) where TWriter : struct, IEntityWriter
        {
            writer.Add<PadHome>();
            writer.Add<Docked>();
            writer.SetEnabled<Docked>(false);
        }

        /// <summary>Fill the returned buffer with one <see cref="LandingPad"/> per pad.</summary>
        public static DynamicBuffer<LandingPad> AddAirfield<TWriter>(ref TWriter writer)
            where TWriter : struct, IEntityWriter =>
            writer.AddBuffer<LandingPad>();
    }
}
