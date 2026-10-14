namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// A set of <see cref="CommandType"/> values, one bit each (every value is below 64).
    /// A value type, so it copies into jobs and Burst code.
    /// </summary>
    public struct TypeSet
    {
        private ulong _bits;

        public static TypeSet Single(CommandType type) => default(TypeSet).With(type);

        /// <summary>Every command type from <paramref name="first"/> to <paramref name="last"/>, both included.</summary>
        public static TypeSet Range(CommandType first, CommandType last)
        {
            var set = default(TypeSet);
            for (var value = first; value <= last; value++)
            {
                set = set.With(value);
            }

            return set;
        }

        public readonly TypeSet With(CommandType type) => new() { _bits = _bits | Bit(type) };

        public readonly TypeSet With(in TypeSet other) => new() { _bits = _bits | other._bits };

        public readonly bool Contains(CommandType type) => (_bits & Bit(type)) != 0;

        private static ulong Bit(CommandType type) => 1ul << (int)type;
    }
}
