using HyperRTS.Simulation.Buildings.Placement;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>How <see cref="EconomyTestKit.PlaceLine"/> starts and queues a line of buildings.</summary>
    public sealed class TestLine
    {
        public LineStart Join = LineStart.Snapped;
        public bool Queue;
    }
}
