using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Adds the components of a neutral, full resource node.</summary>
    public static class ResourceNodeSetup
    {
        public static void Add<TWriter>(ref TWriter writer, UnityObjectRef<ResourceType> type, int amount,
            float regrowthPerSecond) where TWriter : struct, IEntityWriter
        {
            writer.Add(new ResourceNode
            {
                Type = type,
                Amount = amount,
                MaxAmount = amount,
                RegrowthPerSecond = regrowthPerSecond,
            });
            writer.Add(new Faction { Value = Faction.Neutral });
        }
    }
}
