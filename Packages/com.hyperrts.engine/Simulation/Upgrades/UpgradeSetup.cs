using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>Builds an upgrade prefab: identity, research time, cost, prerequisites and effects.</summary>
    public static class UpgradeSetup
    {
        // Each buffer is filled before the next is added: adding one invalidates earlier buffer handles.
        public static void Add<TWriter>(ref TWriter writer, in EntityInfo info, float researchTime,
            IReadOnlyList<ResourceCost> cost, IReadOnlyList<Prerequisite> prerequisites,
            IReadOnlyList<UpgradeEffect> effects) where TWriter : struct, IEntityWriter
        {
            writer.Add<Upgrade>();
            writer.Add(info);
            writer.Add(new Producible { BuildTime = researchTime });

            writer.AddBuffer(cost);
            writer.AddBuffer(prerequisites);
            writer.AddBuffer(effects);
        }
    }
}
