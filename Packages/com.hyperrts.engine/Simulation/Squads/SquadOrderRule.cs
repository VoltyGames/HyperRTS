using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Resources;
using Unity.Entities;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// When a squad may start a Retreat or Reinforce, and what bringing a member back costs. A squad without a
    /// <see cref="ResourceCost"/> is free, which callers pass as an uncreated buffer.
    /// </summary>
    public static class SquadOrderRule
    {
        public static SquadRefusal FindRetreatRefusal(bool hasPoint) =>
            hasPoint ? SquadRefusal.None : SquadRefusal.NoPoint;

        /// <summary>The first thing in the way of reinforcing: nobody dead, no point in range, or too poor.</summary>
        public static SquadRefusal FindReinforceRefusal(DynamicBuffer<SquadMember> members, bool inRange,
            in SquadRules rules, DynamicBuffer<ResourceCost> cost, DynamicBuffer<ResourceStock> stock)
        {
            if (!SquadMember.AnyDead(members))
            {
                return SquadRefusal.FullStrength;
            }

            if (!inRange)
            {
                return SquadRefusal.NoPoint;
            }

            return CanPay(cost, stock, rules.ReinforceShare(members.Length)) ? SquadRefusal.None : SquadRefusal.TooPoor;
        }

        public static bool CanPay(DynamicBuffer<ResourceCost> cost, DynamicBuffer<ResourceStock> stock, float share) =>
            !cost.IsCreated || ResourceMath.CanAfford(stock, cost, share);

        public static bool Pay(DynamicBuffer<ResourceCost> cost, DynamicBuffer<ResourceStock> stock, float share) =>
            !cost.IsCreated || ResourceMath.TrySpend(stock, cost, share);

        public static void Refund(DynamicBuffer<ResourceCost> cost, DynamicBuffer<ResourceStock> stock, float share)
        {
            if (cost.IsCreated)
            {
                ResourceMath.Refund(stock, cost, share);
            }
        }
    }
}
