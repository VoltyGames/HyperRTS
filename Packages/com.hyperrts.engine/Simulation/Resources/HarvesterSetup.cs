using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Adds the harvester components to a unit.</summary>
    public static class HarvesterSetup
    {
        public static void Add<TSink>(ref TSink sink, int capacity, float gatherRate) where TSink : struct, IComponentSink
        {
            sink.Add(new Harvester { Capacity = capacity, GatherRate = gatherRate });
            sink.Add<HarvestState>();
        }
    }
}
