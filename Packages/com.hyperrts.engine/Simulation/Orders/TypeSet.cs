using Unity.Mathematics;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// A set of <see cref="CommandType"/> values, one bit for each of the 256 a byte holds.
    /// A value type, so it copies into jobs and Burst code.
    /// </summary>
    public struct TypeSet
    {
        // Eight 32-bit words: column is word / 4, row is word % 4.
        private uint4x2 _bits;

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
            var word = Word(type);
            set._bits[word >> 2][word & 3] |= Bit(type);
            return set;
        }

        public readonly TypeSet With(in TypeSet other) => new() { _bits = _bits | other._bits };

        public readonly bool Contains(CommandType type)
        {
            var word = Word(type);
            return (_bits[word >> 2][word & 3] & Bit(type)) != 0;
        }

        private static int Word(CommandType type) => (int)type >> 5;

        private static uint Bit(CommandType type) => 1u << ((int)type & 31);
    }
}
