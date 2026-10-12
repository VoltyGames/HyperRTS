using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Adds the share of fallen units' cost an owned entity pays its owner.</summary>
    public static class CasualtyIncomeSetup
    {
        public static void Add<TWriter>(ref TWriter writer, UnityObjectRef<ResourceType> type, float share)
            where TWriter : struct, IEntityWriter =>
            writer.Add(new CasualtyIncome { Type = type, Share = share });
    }
}
