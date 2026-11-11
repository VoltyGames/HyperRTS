using HyperRTS.Simulation.Navigation.Fields;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Sectors
{
    /// <summary>How each class's components join across sector borders into regions.</summary>
    public struct SectorLinks
    {
        private const int East = 0;
        private const int North = 1;

        /// <summary>Edges per class, sector and border (east, north).</summary>
        public NativeArray<byte> EdgeCounts;

        /// <summary>Per class, sector, border and edge: the own label in the low byte, the neighbour's in the high.</summary>
        public NativeArray<ushort> Edges;

        /// <summary>Per class and sector, the id of its first component; ids run on across sectors and classes.</summary>
        public NativeArray<int> Firsts;

        /// <summary>The sector of each component.</summary>
        public NativeList<int> Owners;

        /// <summary>The region of each component: the smallest component id joined to it, plus 1.</summary>
        public NativeList<int> Regions;

        private readonly SectorLayout _layout;
        private readonly int _classes;

        public SectorLinks(int classes, SectorLayout layout, Allocator allocator)
        {
            var count = layout.Total;
            EdgeCounts = new NativeArray<byte>(classes * count * 2, allocator);
            Edges = new NativeArray<ushort>(classes * count * 2 * NavField.SectorCells, allocator);
            Firsts = new NativeArray<int>(classes * count + 1, allocator);
            Owners = new NativeList<int>(allocator);
            Regions = new NativeList<int>(allocator);
            _layout = layout;
            _classes = classes;
        }

        public void Dispose()
        {
            EdgeCounts.Dispose();
            Edges.Dispose();
            Firsts.Dispose();
            Owners.Dispose();
            Regions.Dispose();
        }

        public readonly int Global(int classIndex, int sector, int label) =>
            Firsts[_layout.ClassSector(classIndex, sector)] + label - 1;

        // The sector across a border, east or north.
        private readonly int Across(int sector, int border) => border == East ? sector + 1 : sector + _layout.Count.x;

        /// <summary>Adds the components that share an edge with one, of the same class.</summary>
        public readonly void Neighbours(int classIndex, int component, NativeList<int> into)
        {
            var sector = Owners[component];
            var label = component - Firsts[_layout.ClassSector(classIndex, sector)] + 1;
            var at = _layout.Coordinates(sector);
            AddAcross(classIndex, sector, East, label, true, into);
            AddAcross(classIndex, sector, North, label, true, into);
            if (at.x > 0)
            {
                AddAcross(classIndex, sector - 1, East, label, false, into);
            }

            if (at.y > 0)
            {
                AddAcross(classIndex, sector - _layout.Count.x, North, label, false, into);
            }
        }

        // Edges of one border whose own (or neighbour) end is the label, adding the far end's component.
        private readonly void AddAcross(int classIndex, int sector, int border, int label, bool own,
            NativeList<int> into)
        {
            var other = own ? Across(sector, border) : sector;
            var key = _layout.ClassSector(classIndex, sector) * 2 + border;
            for (var i = 0; i < EdgeCounts[key]; i++)
            {
                var edge = Edges[key * NavField.SectorCells + i];
                var near = own ? edge & 0xff : edge >> 8;
                var far = own ? edge >> 8 : edge & 0xff;
                if (near == label)
                {
                    into.Add(Global(classIndex, other, far));
                }
            }
        }

        /// <summary>
        /// Rebuilds the edges of dirty sectors and of the sectors whose borders they share, renumbers the components
        /// and reruns union-find over every edge.
        /// </summary>
        public void Rebuild(in NativeArray<byte> labels, in NativeArray<byte> counts, in NativeArray<bool> dirty)
        {
            for (var sector = 0; sector < _layout.Total; sector++)
            {
                var at = _layout.Coordinates(sector);
                var east = at.x + 1 < _layout.Count.x && dirty[sector + 1];
                var north = at.y + 1 < _layout.Count.y && dirty[sector + _layout.Count.x];
                if (dirty[sector] || east || north)
                {
                    for (var classIndex = 0; classIndex < _classes; classIndex++)
                    {
                        Border(labels, classIndex, sector, East);
                        Border(labels, classIndex, sector, North);
                    }
                }
            }

            Number(counts);
            Unite();
        }

        // The distinct label pairs facing each other across one border; at most one per border cell.
        private void Border(in NativeArray<byte> labels, int classIndex, int sector, int border)
        {
            var key = _layout.ClassSector(classIndex, sector) * 2 + border;
            var at = _layout.Coordinates(sector);
            var last = NavField.SectorCells - 1;
            var count = 0;
            var other = Across(sector, border);
            if ((border == East && at.x + 1 < _layout.Count.x) || (border == North && at.y + 1 < _layout.Count.y))
            {
                var own = _layout.ClassSector(classIndex, sector) * NavSectors.Area;
                var far = _layout.ClassSector(classIndex, other) * NavSectors.Area;
                for (var i = 0; i < NavField.SectorCells; i++)
                {
                    var near = border == East ? i * NavField.SectorCells + last : last * NavField.SectorCells + i;
                    var across = border == East ? i * NavField.SectorCells : i;
                    var a = labels[own + near];
                    var b = labels[far + across];
                    var edge = (ushort)(a | (b << 8));
                    if (a != 0 && b != 0 && !HasEdge(key, count, edge))
                    {
                        Edges[key * NavField.SectorCells + count++] = edge;
                    }
                }
            }

            EdgeCounts[key] = (byte)count;
        }

        private readonly bool HasEdge(int key, int count, ushort edge)
        {
            for (var i = 0; i < count; i++)
            {
                if (Edges[key * NavField.SectorCells + i] == edge)
                {
                    return true;
                }
            }

            return false;
        }

        private void Number(in NativeArray<byte> counts)
        {
            Owners.Clear();
            var next = 0;
            for (var i = 0; i < _classes * _layout.Total; i++)
            {
                Firsts[i] = next;
                next += counts[i];
                for (var c = 0; c < counts[i]; c++)
                {
                    Owners.Add(i % _layout.Total);
                }
            }

            Firsts[_classes * _layout.Total] = next;
        }

        private void Unite()
        {
            var total = Owners.Length;
            Regions.ResizeUninitialized(total);
            for (var i = 0; i < total; i++)
            {
                Regions[i] = i;
            }

            for (var key = 0; key < EdgeCounts.Length; key++)
            {
                var sector = key / 2 % _layout.Total;
                var classIndex = key / 2 / _layout.Total;
                var other = Across(sector, key % 2);
                for (var i = 0; i < EdgeCounts[key]; i++)
                {
                    var edge = Edges[key * NavField.SectorCells + i];
                    Join(Global(classIndex, sector, edge & 0xff), Global(classIndex, other, edge >> 8));
                }
            }

            for (var i = 0; i < total; i++)
            {
                Regions[i] = Root(i);
            }

            for (var i = 0; i < total; i++)
            {
                Regions[i]++;
            }
        }

        // The smallest id stays the root, so the region ids depend only on the labels, not the edge order.
        private void Join(int a, int b)
        {
            a = Root(a);
            b = Root(b);
            if (a != b)
            {
                Regions[math.max(a, b)] = math.min(a, b);
            }
        }

        private int Root(int component)
        {
            while (Regions[component] != component)
            {
                var parent = Regions[component];
                Regions[component] = Regions[parent];
                component = parent;
            }

            return component;
        }
    }
}
