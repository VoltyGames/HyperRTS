using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Air
{
    /// <summary>Adds the flight components on top of a unit set up on the <c>Air</c> nav layer.</summary>
    public static class AirSetup
    {
        public static void AddFlight<TSink>(ref TSink sink, in Flight flight) where TSink : struct, IComponentSink
        {
            sink.Add(flight);
        }
    }
}
