using Unity.Mathematics;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>A set of byte values, one bit for each of the 256. A value type, so it copies into jobs.</summary>
    public struct ByteSet
    {
        // Eight 32-bit words: column is word / 4, row is word % 4.
        private uint4x2 _bits;

        public void Add(byte value)
        {
            var word = value >> 5;
            _bits[word >> 2][word & 3] |= Bit(value);
        }

        public readonly ByteSet With(in ByteSet other) => new() { _bits = _bits | other._bits };

        public readonly bool Contains(byte value)
        {
            var word = value >> 5;
            return (_bits[word >> 2][word & 3] & Bit(value)) != 0;
        }

        private static uint Bit(byte value) => 1u << (value & 31);
    }
}
