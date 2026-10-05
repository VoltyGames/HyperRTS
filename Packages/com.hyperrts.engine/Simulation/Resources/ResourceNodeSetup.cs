using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Adds the components of a neutral, full resource node.</summary>
    public static class ResourceNodeSetup
    {
        public static void Add<TSink>(ref TSink sink, UnityObjectRef<ResourceType> type, int amount,
            float regrowthPerSecond) where TSink : struct, IComponentSink
        {
            sink.Add(new ResourceNode
            {
                Type = type,
                Amount = amount,
                MaxAmount = amount,
                RegrowthPerSecond = regrowthPerSecond,
            });
            sink.Add(new Faction { Value = Faction.Neutral });
        }
    }
}
