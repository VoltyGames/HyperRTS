namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// A set of <see cref="CommandType"/> values, one bit for each of the 256 a byte holds.
    /// A value type, so it copies into jobs and Burst code.
    /// </summary>
    public struct TypeSet
    {
        private ulong _bits0;
        private ulong _bits1;
        private ulong _bits2;
        private ulong _bits3;

        public static TypeSet Single(CommandType type) => default(TypeSet).With(type);

        /// <summary>Every command type from <paramref name="first"/> to <paramref name="last"/>, both included.</summary>
        public static TypeSet Range(CommandType first, CommandType last)
        {
            var set = default(TypeSet);
            // An int counter, since a byte one wraps past 255 and never ends.
            for (var value = (int)first; value <= (int)last; value++)
            {
                set = set.With((CommandType)value);
            }

            return set;
        }

        public readonly TypeSet With(CommandType type)
        {
            var set = this;
            var bit = Bit(type);
            switch (Word(type))
            {
                case 0:
                    set._bits0 |= bit;
                    break;
                case 1:
                    set._bits1 |= bit;
                    break;
                case 2:
                    set._bits2 |= bit;
                    break;
                default:
                    set._bits3 |= bit;
                    break;
            }

            return set;
        }

        public readonly TypeSet With(in TypeSet other) => new()
        {
            _bits0 = _bits0 | other._bits0,
            _bits1 = _bits1 | other._bits1,
            _bits2 = _bits2 | other._bits2,
            _bits3 = _bits3 | other._bits3,
        };

        public readonly bool Contains(CommandType type) => (Bits(Word(type)) & Bit(type)) != 0;

        private readonly ulong Bits(int word) => word switch
        {
            0 => _bits0,
            1 => _bits1,
            2 => _bits2,
            _ => _bits3,
        };

        private static int Word(CommandType type) => (int)type >> 6;

        private static ulong Bit(CommandType type) => 1ul << ((int)type & 63);
    }
}
