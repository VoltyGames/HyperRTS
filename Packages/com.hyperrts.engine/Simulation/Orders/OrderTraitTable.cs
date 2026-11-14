using System;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Traits per order type: fixed for engine types, registered by the game for its own, with the game command that
    /// issues each game order. A singleton that <see cref="OrderDispatchSystem"/> creates, so a game registers its
    /// types from a system update.
    /// </summary>
    public struct OrderTraitTable : IComponentData
    {
        private const int GameTypes = 256 - (int)OrderType.GameFirst;

        private const OrderTraits Walks = OrderTraits.Formation | OrderTraits.MovesToPosition;

        private const int GameCommands = 256 - (int)CommandType.GameFirst;

        private FixedList512Bytes<OrderTraits> _game;

        // The order each game command issues; None for commands the game handles itself.
        private FixedList512Bytes<OrderType> _commands;

        // So a type registered with no traits still counts as registered.
        private ByteSet _registered;

        public static OrderTraitTable Create() => new()
        {
            _game = new FixedList512Bytes<OrderTraits> { Length = GameTypes },
            _commands = new FixedList512Bytes<OrderType> { Length = GameCommands },
        };

        /// <summary>Sets the traits of a game order type; engine types keep their own.</summary>
        public void Register(OrderType type, OrderTraits traits)
        {
            if (!IsGameType(type))
            {
                throw new ArgumentOutOfRangeException(nameof(type),
                    "HyperRTS: only game order types take registered traits.");
            }

            _game[GameIndex(type)] = traits;
            _registered.Add((byte)type);
        }

        /// <summary>
        /// Makes the game command <paramref name="command"/> issue the registered game order <paramref name="order"/>
        /// to the commanded units, in formation and Shift-queued like an engine order.
        /// </summary>
        public void RegisterCommand(CommandType command, OrderType order)
        {
            if (command < CommandType.GameFirst)
            {
                throw new ArgumentOutOfRangeException(nameof(command),
                    "HyperRTS: only game commands map to registered orders.");
            }

            if (!IsGameType(order) || !IsRegistered(order))
            {
                throw new InvalidOperationException("HyperRTS: a game command maps only to a registered game order.");
            }

            _commands[(int)command - (int)CommandType.GameFirst] = order;
        }

        /// <summary>The game order <paramref name="command"/> issues, if the game registered one.</summary>
        public readonly bool TryGetOrder(CommandType command, out OrderType order)
        {
            order = command >= CommandType.GameFirst
                ? _commands[(int)command - (int)CommandType.GameFirst]
                : OrderType.None;
            return order != OrderType.None;
        }

        public readonly bool IsRegistered(OrderType type) => !IsGameType(type) || _registered.Contains((byte)type);

        public readonly OrderTraits Traits(OrderType type) =>
            IsGameType(type) ? _game[GameIndex(type)] : EngineTraits(type);

        public readonly bool Has(OrderType type, OrderTraits traits) => (Traits(type) & traits) != 0;

        private static bool IsGameType(OrderType type) => type >= OrderType.GameFirst;

        private static int GameIndex(OrderType type) => (int)type - (int)OrderType.GameFirst;

        private static OrderTraits EngineTraits(OrderType type) => type switch
        {
            OrderType.Move => Walks,
            OrderType.ReverseMove => Walks | OrderTraits.DrivesBackward,
            OrderType.AttackMove => Walks | OrderTraits.EngagesWhileMoving,
            OrderType.Patrol => Walks | OrderTraits.EngagesWhileMoving | OrderTraits.Loops,
            OrderType.Escort => OrderTraits.EngagesWhileMoving,
            _ => OrderTraits.None,
        };
    }
}
