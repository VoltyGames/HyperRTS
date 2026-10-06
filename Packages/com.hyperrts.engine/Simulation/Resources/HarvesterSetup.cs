using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Adds the harvester components to a unit.</summary>
    public static class HarvesterSetup
    {
        public static void Add<TWriter>(ref TWriter writer, int capacity, float gatherRate)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Harvester { Capacity = capacity, GatherRate = gatherRate });
            writer.Add<HarvestState>();
        }
    }
}
