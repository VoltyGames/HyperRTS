using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// The sectors a search may enter, each given a slot, so search records index by slot and local cell instead of
    /// spanning the grid. Unsafe lists, so a stored flow field can keep one inside a native list.
    /// </summary>
    public struct NavCorridor : IDisposable
    {
        // Per sector: its slot, or -1 outside.
        private UnsafeList<int> _slots;
        private UnsafeList<int> _sectors;
        private SectorLayout _layout;

        public NavCorridor(SectorLayout layout, Allocator allocator)
        {
            _layout = layout;
            _slots = new UnsafeList<int>(layout.Total, allocator);
            _slots.Resize(layout.Total);
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i] = -1;
            }

            _sectors = new UnsafeList<int>(16, allocator);
        }

        /// <summary>Every sector of the grid, for searches that may go anywhere (editor tools).</summary>
        public static NavCorridor Whole(SectorLayout layout, Allocator allocator)
        {
            var corridor = new NavCorridor(layout, allocator);
            for (var sector = 0; sector < layout.Total; sector++)
            {
                corridor.Add(sector);
            }

            return corridor;
        }

        public readonly int SectorCount => _sectors.Length;

        /// <summary>Cells the records cover.</summary>
        public readonly int Cells => _sectors.Length * NavSectors.Area;

        public readonly int Sector(int slot) => _sectors[slot];

        public void Dispose()
        {
            _slots.Dispose();
            _sectors.Dispose();
        }

        public void Add(int sector)
        {
            if (_slots[sector] < 0)
            {
                _slots[sector] = _sectors.Length;
                _sectors.Add(sector);
            }
        }

        /// <summary>Adds a sector and the eight round it.</summary>
        public void AddWithRing(int sector)
        {
            var at = _layout.Coordinates(sector);
            var low = math.max(at - 1, 0);
            var high = math.min(at + 1, _layout.Count - 1);
            for (var y = low.y; y <= high.y; y++)
            {
                for (var x = low.x; x <= high.x; x++)
                {
                    Add(y * _layout.Count.x + x);
                }
            }
        }

        /// <summary>Adds each sector and the eight round it.</summary>
        public void AddWithRings(NativeArray<int> sectors)
        {
            foreach (var sector in sectors)
            {
                AddWithRing(sector);
            }
        }

        /// <summary>Drops the sectors added after the first <paramref name="count"/>.</summary>
        public void Truncate(int count)
        {
            for (var slot = count; slot < _sectors.Length; slot++)
            {
                _slots[_sectors[slot]] = -1;
            }

            _sectors.Length = count;
        }

        /// <summary>The record index of a cell, or -1 outside the corridor.</summary>
        public readonly int Index(int2 cell)
        {
            var sector = _layout.Index(cell);
            var slot = sector < 0 ? -1 : _slots[sector];
            return slot < 0 ? -1 : slot * NavSectors.Area + NavSectors.Local(cell);
        }

        /// <summary>The record index of a cell's neighbour; a step inside the cell's sector skips the lookup.</summary>
        public readonly int Index(int index, int2 cell, int2 offset)
        {
            var local = cell % NavField.SectorCells + offset;
            var inside = math.all(local >= 0 & local < NavField.SectorCells);
            return inside ? index + offset.y * NavField.SectorCells + offset.x : Index(cell + offset);
        }

        public readonly int2 Cell(int index) =>
            _layout.Cell(_sectors[index / NavSectors.Area], index % NavSectors.Area);

        /// <summary>Whether a box of cells, grown by <paramref name="halo"/>, reaches any corridor sector.</summary>
        public readonly bool Touches(int2 min, int2 max, int halo)
        {
            _layout.Range(min, max, halo, out var low, out var high);
            for (var y = low.y; y <= high.y; y++)
            {
                for (var x = low.x; x <= high.x; x++)
                {
                    if (_slots[y * _layout.Count.x + x] >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
