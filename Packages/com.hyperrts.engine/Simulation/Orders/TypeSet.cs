namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// A set of byte-sized type values (<see cref="CommandType"/>, <see cref="OrderType"/>), custom ones included.
    /// A value type, so it copies into jobs and Burst code.
    /// </summary>
    public struct TypeSet
    {
        private ulong _bits0;
        private ulong _bits1;
        private ulong _bits2;
        private ulong _bits3;

        public static TypeSet Of(CommandType type) => default(TypeSet).With(type);

        /// <summary>Every command type from <paramref name="first"/> to <paramref name="last"/>, both included.</summary>
        public static TypeSet Range(CommandType first, CommandType last)
        {
            var set = default(TypeSet);
            for (var value = (int)first; value <= (int)last; value++)
            {
                set.Add((byte)value);
            }

            return set;
        }

        public readonly TypeSet With(CommandType type) => With((byte)type);

        public readonly TypeSet With(OrderType type) => With((byte)type);

        public readonly TypeSet With(in TypeSet other)
        {
            var set = this;
            set._bits0 |= other._bits0;
            set._bits1 |= other._bits1;
            set._bits2 |= other._bits2;
            set._bits3 |= other._bits3;
            return set;
        }

        public void Add(byte value)
        {
            var bit = 1ul << (value & 63);
            switch (value >> 6)
            {
                case 0: _bits0 |= bit; break;
                case 1: _bits1 |= bit; break;
                case 2: _bits2 |= bit; break;
                default: _bits3 |= bit; break;
            }
        }

        public readonly bool Contains(CommandType type) => Contains((byte)type);

        public readonly bool Contains(OrderType type) => Contains((byte)type);

        public readonly bool Contains(byte value)
        {
            var bits = (value >> 6) switch
            {
                0 => _bits0,
                1 => _bits1,
                2 => _bits2,
                _ => _bits3,
            };
            return (bits & (1ul << (value & 63))) != 0;
        }

        private readonly TypeSet With(byte value)
        {
            var set = this;
            set.Add(value);
            return set;
        }
    }
}
