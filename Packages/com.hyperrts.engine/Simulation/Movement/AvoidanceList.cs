using Unity.Collections;


namespace HyperRTS.Simulation.Movement
{
    /// <summary>The nearest few neighbours a mover steers round, dropping the farthest when full.</summary>
    internal struct AvoidanceList
    {
        public const int Capacity = 8;

        private FixedList512Bytes<AvoidanceNeighbour> _items;

        public readonly FixedList512Bytes<AvoidanceNeighbour> Items => _items;

        public readonly int Count => _items.Length;

        public void Keep(in AvoidanceNeighbour neighbour)
        {
            if (_items.Length < Capacity)
            {
                _items.Add(neighbour);
                return;
            }

            var farthest = 0;
            for (var i = 1; i < _items.Length; i++)
            {
                farthest = _items[i].DistanceSq > _items[farthest].DistanceSq ? i : farthest;
            }

            if (neighbour.DistanceSq < _items[farthest].DistanceSq)
            {
                _items[farthest] = neighbour;
            }
        }
    }
}
