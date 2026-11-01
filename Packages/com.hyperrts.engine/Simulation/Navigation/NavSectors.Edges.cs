using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation
{
    public partial struct NavSectors
    {
        private const int East = 0;
        private const int North = 1;

        private static readonly ProfilerMarker ConnectMarker = new("NavSectors.Connect");

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

        public readonly int Global(int classIndex, int sector, int label) =>
            Firsts[ClassSector(classIndex, sector)] + label - 1;

        // The sector across a border, east or north.
        private readonly int Across(int sector, int border) => border == East ? sector + 1 : sector + Layout.Count.x;

        /// <summary>Adds the components that share an edge with one, of the same class.</summary>
        public readonly void Neighbours(int classIndex, int component, NativeList<int> into)
        {
            var sector = Owners[component];
            var label = component - Firsts[ClassSector(classIndex, sector)] + 1;
            var at = Layout.Coordinates(sector);
            AddAcross(classIndex, sector, East, label, true, into);
            AddAcross(classIndex, sector, North, label, true, into);
            if (at.x > 0)
            {
                AddAcross(classIndex, sector - 1, East, label, false, into);
            }

            if (at.y > 0)
            {
                AddAcross(classIndex, sector - Layout.Count.x, North, label, false, into);
            }
        }

        // Edges of one border whose own (or neighbour) end is the label, adding the far end's component.
        private readonly void AddAcross(int classIndex, int sector, int border, int label, bool own,
            NativeList<int> into)
        {
            var other = own ? Across(sector, border) : sector;
            var key = ClassSector(classIndex, sector) * 2 + border;
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
        /// and reruns union-find over every edge, then clears the dirty marks.
        /// </summary>
        public void Connect()
        {
            using var scope = ConnectMarker.Auto();
            for (var sector = 0; sector < SectorCount; sector++)
            {
                var at = Layout.Coordinates(sector);
                var east = at.x + 1 < Layout.Count.x && Dirty[sector + 1];
                var north = at.y + 1 < Layout.Count.y && Dirty[sector + Layout.Count.x];
                if (Dirty[sector] || east || north)
                {
                    for (var classIndex = 0; classIndex < ClassCount; classIndex++)
                    {
                        Border(classIndex, sector, East);
                        Border(classIndex, sector, North);
                    }
                }
            }

            Number();
            Unite();
            for (var i = 0; i < Dirty.Length; i++)
            {
                Dirty[i] = false;
            }
        }

        // The distinct label pairs facing each other across one border; at most one per border cell.
        private void Border(int classIndex, int sector, int border)
        {
            var key = ClassSector(classIndex, sector) * 2 + border;
            var at = Layout.Coordinates(sector);
            var last = NavField.SectorCells - 1;
            var count = 0;
            var other = Across(sector, border);
            if ((border == East && at.x + 1 < Layout.Count.x) || (border == North && at.y + 1 < Layout.Count.y))
            {
                var own = ClassSector(classIndex, sector) * Area;
                var far = ClassSector(classIndex, other) * Area;
                for (var i = 0; i < NavField.SectorCells; i++)
                {
                    var near = border == East ? i * NavField.SectorCells + last : last * NavField.SectorCells + i;
                    var across = border == East ? i * NavField.SectorCells : i;
                    var a = Labels[own + near];
                    var b = Labels[far + across];
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

        private void Number()
        {
            Owners.Clear();
            var next = 0;
            for (var i = 0; i < ClassCount * SectorCount; i++)
            {
                Firsts[i] = next;
                next += Counts[i];
                for (var c = 0; c < Counts[i]; c++)
                {
                    Owners.Add(i % SectorCount);
                }
            }

            Firsts[ClassCount * SectorCount] = next;
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
                var sector = key / 2 % SectorCount;
                var classIndex = key / 2 / SectorCount;
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
