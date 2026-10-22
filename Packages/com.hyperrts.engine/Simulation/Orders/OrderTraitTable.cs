using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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

        // One bit per game type, so a type registered with no traits still counts as registered.
        private uint4 _registered;

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

            var index = GameIndex(type);
            _game[index] = traits;
            _registered[index >> 5] |= 1u << (index & 31);
        }

        public readonly bool IsRegistered(OrderType type)
        {
            if (!IsGameType(type))
            {
                return true;
            }

            var index = GameIndex(type);
            return (_registered[index >> 5] & (1u << (index & 31))) != 0;
        }

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
            _ => OrderTraits.None,
        };
    }
}
