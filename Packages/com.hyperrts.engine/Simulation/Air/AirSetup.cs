using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Air
{
    /// <summary>Adds flight and pad use to aircraft and pads to airfields.</summary>
    public static class AirSetup
    {
        /// <summary>
        /// Flies the unit at <paramref name="altitude"/>; its <c>NavAgent</c> must be on the Air layer. Its remains
        /// fall to the ground. Drops leave from <paramref name="releasePoint"/>, in its local space.
        /// </summary>
        public static void AddFlight<TWriter>(ref TWriter writer, float altitude, float climbSpeed, float loiterRadius,
            float3 releasePoint = default)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Flight { Altitude = altitude, ClimbSpeed = climbSpeed, LoiterRadius = loiterRadius });
            writer.Add(new DropRun { ReleasePoint = releasePoint });
            writer.Add<DropRemains>();
        }

        /// <summary>The aircraft takes a pad of <paramref name="kind"/> at the airfield that builds it and docks there to reload.</summary>
        public static void AddPadUser<TWriter>(ref TWriter writer, PadKind kind) where TWriter : struct, IEntityWriter
        {
            writer.Add(new HomePad { Kind = kind });
            writer.Add<Docked>();
            writer.SetEnabled<Docked>(false);
        }

        /// <summary>Fill the returned buffer with one <see cref="LandingPad"/> per pad.</summary>
        public static DynamicBuffer<LandingPad> AddAirfield<TWriter>(ref TWriter writer)
            where TWriter : struct, IEntityWriter =>
            writer.AddBuffer<LandingPad>();
    }
}
