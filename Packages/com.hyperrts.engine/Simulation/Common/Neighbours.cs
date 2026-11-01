using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>The offsets to a cell's eight neighbours, row by row from the one below-left.</summary>
    public readonly struct Neighbours
    {
        public Enumerator GetEnumerator() => default;

        public struct Enumerator
        {
            // One past the 3 x 3 slot of the current offset, so the default starts before the first.
            private int _slot;

            public readonly int2 Current => new((_slot - 1) % 3 - 1, (_slot - 1) / 3 - 1);

            public bool MoveNext()
            {
                _slot++;
                if (_slot == 5)
                {
                    _slot++;
                }

                return _slot <= 9;
            }
        }
    }
}
