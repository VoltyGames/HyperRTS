using HyperRTS.Simulation.Orders;
using Unity.Collections;

namespace HyperRTS.Simulation.Commands
{
    /// <summary>
    /// The orders a command may give a unit, best first, and the <see cref="Fallback"/> it gets when it can take
    /// none of them. <see cref="For"/> is the one table of which engine command gives which order.
    /// </summary>
    public struct CommandOrders
    {
        public FixedList32Bytes<OrderType> Choices;

        /// <summary>Move, so the unit still goes where it was sent, or None, so it ignores the command.</summary>
        public OrderType Fallback;

        /// <summary>A game command gives the order registered for it in <paramref name="traits"/>, if any.</summary>
        public static CommandOrders ForGame(CommandType command, in OrderTraitTable traits) =>
            traits.TryGetOrder(command, out var order) ? Try(order) : default;

        /// <summary>What an engine command gives; Smart is the right-click priority list.</summary>
        public static CommandOrders For(CommandType command)
        {
            return command switch
            {
                CommandType.Smart => Try(OrderType.Attack).Then(OrderType.Gather).Then(OrderType.Build)
                    .Then(OrderType.Repair).Then(OrderType.Enter).Then(OrderType.ReturnToBase)
                    .Then(OrderType.Capture).Else(OrderType.Move),
                CommandType.Move => Try(OrderType.Move),
                // Attack-move clicked on an enemy or on scenery attacks it directly.
                CommandType.AttackMove => Try(OrderType.Attack).Then(OrderType.AttackMove).Else(OrderType.Move),
                CommandType.Attack => Try(OrderType.Attack).Else(OrderType.Move),
                CommandType.FireMission => Try(OrderType.FireMission).Else(OrderType.None),
                CommandType.Patrol => Try(OrderType.Patrol),
                CommandType.ReverseMove => Try(OrderType.ReverseMove),
                CommandType.Escort => Try(OrderType.Escort).Else(OrderType.Move),
                CommandType.ReturnToBase => Try(OrderType.ReturnToBase).Else(OrderType.None),
                CommandType.Gather => Try(OrderType.Gather).Else(OrderType.Move),
                CommandType.Build => Try(OrderType.Build).Else(OrderType.Move),
                CommandType.Repair => Try(OrderType.Repair).Else(OrderType.Move),
                CommandType.Capture => Try(OrderType.Capture).Else(OrderType.Move),
                CommandType.Enter => Try(OrderType.Enter).Else(OrderType.Move),
                _ => default,
            };
        }

        private static CommandOrders Try(OrderType order) => default(CommandOrders).Then(order);

        private readonly CommandOrders Then(OrderType order)
        {
            var orders = this;
            orders.Choices.Add(order);
            return orders;
        }

        private readonly CommandOrders Else(OrderType fallback)
        {
            var orders = this;
            orders.Fallback = fallback;
            return orders;
        }
    }
}
