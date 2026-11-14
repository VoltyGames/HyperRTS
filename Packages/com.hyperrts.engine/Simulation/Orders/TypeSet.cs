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

        public readonly TypeSet With(CommandType type)
        {
            var set = this;
            set._bits.Add((byte)type);
            return set;
        }

        public readonly bool Contains(CommandType type) => _bits.Contains((byte)type);
    }
}
