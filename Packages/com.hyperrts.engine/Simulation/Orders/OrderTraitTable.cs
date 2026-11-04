using System;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Traits per order type: fixed for engine types, registered by the game for its own. A singleton that
    /// <see cref="OrderDispatchSystem"/> creates, so a game registers its types from a system update.
    /// </summary>
    public struct OrderTraitTable : IComponentData
    {
        private const int GameTypes = 256 - (int)OrderType.GameFirst;

        private const OrderTraits Walks = OrderTraits.Formation | OrderTraits.MovesToPosition;
        private const OrderTraits Fights = OrderTraits.AttackMove | OrderTraits.EngagesWhileMoving;

        private FixedList512Bytes<OrderTraits> _game;

        // So a type registered with no traits still counts as registered.
        private ByteSet _registered;

        public static OrderTraitTable Create() => new()
        {
            _game = new FixedList512Bytes<OrderTraits> { Length = GameTypes },
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

        public readonly bool IsRegistered(OrderType type) => !IsGameType(type) || _registered.Contains((byte)type);

        public readonly OrderTraits Traits(OrderType type) =>
            IsGameType(type) ? _game[GameIndex(type)] : EngineTraits(type);

        public readonly bool Has(OrderType type, OrderTraits traits) => (Traits(type) & traits) != 0;

        private static bool IsGameType(OrderType type) => type >= OrderType.GameFirst;

        private static int GameIndex(OrderType type) => (int)type - (int)OrderType.GameFirst;

        private static OrderTraits EngineTraits(OrderType type) => type switch
        {
            OrderType.Move => Walks,
            OrderType.AttackMove => Walks | Fights,
            OrderType.Patrol => Walks | Fights | OrderTraits.Loops,
            OrderType.Escort => OrderTraits.EngagesWhileMoving,
            OrderType.Retreat => OrderTraits.MovesToPosition,
            _ => OrderTraits.None,
        };
    }
}
