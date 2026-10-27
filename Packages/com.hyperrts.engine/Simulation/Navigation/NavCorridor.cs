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
        private int2 _sectorCount;

        public NavCorridor(int2 sectors, Allocator allocator)
        {
            _sectorCount = sectors;
            _slots = new UnsafeList<int>(sectors.x * sectors.y, allocator);
            _slots.Resize(sectors.x * sectors.y);
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i] = -1;
            }

            _sectors = new UnsafeList<int>(16, allocator);
        }

        /// <summary>Every sector of the grid, for searches that may go anywhere (editor tools).</summary>
        public static NavCorridor Whole(int2 sectors, Allocator allocator)
        {
            var corridor = new NavCorridor(sectors, allocator);
            for (var sector = 0; sector < sectors.x * sectors.y; sector++)
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
            var at = NavSectors.Coordinates(sector, _sectorCount);
            var low = math.max(at - 1, 0);
            var high = math.min(at + 1, _sectorCount - 1);
            for (var y = low.y; y <= high.y; y++)
            {
                for (var x = low.x; x <= high.x; x++)
                {
                    Add(y * _sectorCount.x + x);
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
            var at = cell / NavField.SectorCells;
            if (math.any(cell < 0 | at >= _sectorCount))
            {
                return -1;
            }

            var slot = _slots[at.y * _sectorCount.x + at.x];
            return slot < 0 ? -1 : slot * NavSectors.Area + NavSectors.Local(cell);
        }

        /// <summary>The record index of a cell's neighbour; a step inside the cell's sector skips the lookup.</summary>
        public readonly int Index(int index, int2 cell, int2 offset)
        {
            var local = cell % NavField.SectorCells + offset;
            var inside = math.all(local >= 0 & local < NavField.SectorCells);
            return inside ? index + offset.y * NavField.SectorCells + offset.x : Index(cell + offset);
        }

        public readonly int2 Cell(int index)
        {
            var sector = _sectors[index / NavSectors.Area];
            var local = index % NavSectors.Area;
            var origin = NavSectors.Coordinates(sector, _sectorCount) * NavField.SectorCells;
            return origin + new int2(local % NavField.SectorCells, local / NavField.SectorCells);
        }

        /// <summary>Whether a box of cells, grown by <paramref name="halo"/>, reaches any corridor sector.</summary>
        public readonly bool Touches(int2 min, int2 max, int halo)
        {
            var low = math.max((min - halo) / NavField.SectorCells, 0);
            var high = math.min((max + halo) / NavField.SectorCells, _sectorCount - 1);
            for (var y = low.y; y <= high.y; y++)
            {
                for (var x = low.x; x <= high.x; x++)
                {
                    if (_slots[y * _sectorCount.x + x] >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
