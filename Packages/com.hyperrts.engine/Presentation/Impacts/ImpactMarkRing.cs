namespace HyperRTS.Presentation.Impacts
{
    /// <summary>
    /// One mark kind's slots as a ring: each new mark takes the next slot, so once the kind's cap is reached the oldest
    /// mark is reused. Remembers when each was placed, so its fade follows from the clock.
    /// </summary>
    public sealed class ImpactMarkRing
    {
        private readonly float[] _placed;
        private int _next;

        public ImpactMarkRing(int capacity) => _placed = new float[capacity];

        public int Capacity => _placed.Length;

        /// <summary>Slots holding a mark, from 0; the ring fills before it wraps.</summary>
        public int Count { get; private set; }

        /// <summary>The slot for a mark placed at <paramref name="now"/>: the next free one, else the oldest.</summary>
        public int Place(float now)
        {
            var slot = _next;
            _placed[slot] = now;
            _next = (_next + 1) % Capacity;
            Count = Count < Capacity ? Count + 1 : Capacity;
            return slot;
        }

        public float Age(int slot, float now) => now - _placed[slot];

        public void Clear()
        {
            _next = 0;
            Count = 0;
        }
    }
}
