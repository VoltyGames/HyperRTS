using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Adds the passive income an owned entity pays its owner.</summary>
    public static class ResourceIncomeSetup
    {
        public static void Add<TWriter>(ref TWriter writer, UnityObjectRef<ResourceType> type, int amount,
            float interval, int firstOwnerBonus) where TWriter : struct, IEntityWriter =>
            writer.Add(new ResourceIncome
            {
                Type = type,
                Amount = amount,
                Interval = interval,
                FirstOwnerBonus = firstOwnerBonus,
            });
    }
}
