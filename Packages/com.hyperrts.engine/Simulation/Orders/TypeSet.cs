namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// A set of <see cref="CommandType"/> values, one bit for each of the 256 a byte holds.
    /// A value type, so it copies into jobs and Burst code.
    /// </summary>
    public struct TypeSet
    {
        private ByteSet _bits;

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
            set._bits.Add((byte)type);
            return set;
        }

        public readonly TypeSet With(in TypeSet other) => new() { _bits = _bits.With(other._bits) };

        public readonly bool Contains(CommandType type) => _bits.Contains((byte)type);
    }
}
