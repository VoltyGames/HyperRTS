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
            if (!new SquadRoster(members).AnyDead())
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
            !cost.IsCreated || new Stockpile(stock).CanAfford(cost, share: share);

        public static bool Pay(DynamicBuffer<ResourceCost> cost, DynamicBuffer<ResourceStock> stock, float share) =>
            !cost.IsCreated || new Stockpile(stock).TrySpend(cost, share);

        public static void Refund(DynamicBuffer<ResourceCost> cost, DynamicBuffer<ResourceStock> stock, float share)
        {
            if (cost.IsCreated)
            {
                new Stockpile(stock).Refund(cost, share);
            }
        }
    }
}
