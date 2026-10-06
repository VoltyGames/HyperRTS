using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Air
{
    /// <summary>Adds pad use to aircraft and pads to airfields.</summary>
    public static class AirSetup
    {
        /// <summary>The aircraft takes a pad at the airfield that builds it and docks there to rearm.</summary>
        public static void AddPadUser<TSink>(ref TSink sink) where TSink : struct, IComponentSink
        {
            sink.Add<PadHome>();
            sink.Add<Docked>();
            sink.SetEnabled<Docked>(false);
        }

        /// <summary>Fill the returned buffer with one <see cref="LandingPad"/> per pad.</summary>
        public static DynamicBuffer<LandingPad> AddAirfield<TSink>(ref TSink sink) where TSink : struct, IComponentSink =>
            sink.AddBuffer<LandingPad>();
    }
}
