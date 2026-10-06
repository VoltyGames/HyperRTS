using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;

namespace HyperRTS.Simulation.Upgrades
{
    /// <summary>Builds an upgrade prefab: identity, research time, cost, prerequisites and effects.</summary>
    public static class UpgradeSetup
    {
        // Each buffer is filled before the next is added: adding one invalidates earlier buffer handles.
        public static void Add<TSink>(ref TSink sink, in EntityInfo info, float researchTime,
            IReadOnlyList<ResourceCost> cost, IReadOnlyList<Prerequisite> prerequisites,
            IReadOnlyList<UpgradeEffect> effects) where TSink : struct, IComponentSink
        {
            sink.Add<Upgrade>();
            sink.Add(info);
            sink.Add(new Producible { BuildTime = researchTime });

            var costs = sink.AddBuffer<ResourceCost>();
            foreach (var item in cost)
            {
                costs.Add(item);
            }

            var required = sink.AddBuffer<Prerequisite>();
            foreach (var item in prerequisites)
            {
                required.Add(item);
            }

            var granted = sink.AddBuffer<UpgradeEffect>();
            foreach (var item in effects)
            {
                granted.Add(item);
            }
        }
    }
}
