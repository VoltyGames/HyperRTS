using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation
{
    public partial struct NavSectors
    {
        private const int East = 0;
        private const int North = 1;

        private static readonly ProfilerMarker RelabelMarker = new("NavSectors.Relabel");
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
            Firsts[classIndex * SectorCount + sector] + label - 1;

        /// <summary>Adds the components that share an edge with one, of the same class.</summary>
        public readonly void Neighbours(int classIndex, int component, NativeList<int> into)
        {
            var sector = Owners[component];
            var label = component - Firsts[classIndex * SectorCount + sector] + 1;
            var at = new int2(sector % Sectors.x, sector / Sectors.x);
            AddAcross(classIndex, sector, East, label, true, sector + 1, into);
            AddAcross(classIndex, sector, North, label, true, sector + Sectors.x, into);
            if (at.x > 0)
            {
                AddAcross(classIndex, sector - 1, East, label, false, sector - 1, into);
            }

            if (at.y > 0)
            {
                AddAcross(classIndex, sector - Sectors.x, North, label, false, sector - Sectors.x, into);
            }
        }

        // Edges of one border whose own (or neighbour) end is the label, adding the far end's component in other.
        private readonly void AddAcross(int classIndex, int sector, int border, int label, bool own, int other,
            NativeList<int> into)
        {
            var key = (classIndex * SectorCount + sector) * 2 + border;
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
                var at = new int2(sector % Sectors.x, sector / Sectors.x);
                var east = at.x + 1 < Sectors.x && Dirty[sector + 1];
                var north = at.y + 1 < Sectors.y && Dirty[sector + Sectors.x];
                if (Dirty[sector] || east || north)
                {
                    for (var classIndex = 0; classIndex < Classes; classIndex++)
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
            var key = (classIndex * SectorCount + sector) * 2 + border;
            var at = new int2(sector % Sectors.x, sector / Sectors.x);
            var last = NavField.SectorCells - 1;
            var count = 0;
            var other = border == East ? sector + 1 : sector + Sectors.x;
            if ((border == East && at.x + 1 < Sectors.x) || (border == North && at.y + 1 < Sectors.y))
            {
                var own = (classIndex * SectorCount + sector) * Area;
                var far = (classIndex * SectorCount + other) * Area;
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
            for (var i = 0; i < Classes * SectorCount; i++)
            {
                Firsts[i] = next;
                next += Counts[i];
                for (var c = 0; c < Counts[i]; c++)
                {
                    Owners.Add(i % SectorCount);
                }
            }

            Firsts[Classes * SectorCount] = next;
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
                var other = key % 2 == East ? sector + 1 : sector + Sectors.x;
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

        /// <summary>Relabels the dirty sectors of every class at once, one job each, then connects them.</summary>
        public readonly JobHandle Schedule(in NavField field, in MovementClasses classes, JobHandle dependency)
        {
            var work = new NativeList<int2>(Dirty.Length, Allocator.TempJob);
            for (var sector = 0; sector < Dirty.Length; sector++)
            {
                for (var classIndex = 0; Dirty[sector] && classIndex < Classes; classIndex++)
                {
                    work.Add(new int2(sector, classIndex));
                }
            }

            dependency = new RelabelJob
            {
                Field = field, Classes = classes, Work = work.AsArray(), Labels = Labels, Counts = Counts,
                Representatives = Representatives, Sectors = Sectors, ClassCount = Classes,
            }.Schedule(work.Length, 4, dependency);
            dependency = new ConnectJob { Graph = this }.Schedule(dependency);
            return work.Dispose(dependency);
        }

        /// <summary>One sector of one class per index; each writes only that sector's entries of that class.</summary>
        [BurstCompile]
        private struct RelabelJob : IJobParallelFor
        {
            [ReadOnly] public NavField Field;
            [ReadOnly] public MovementClasses Classes;

            /// <summary>Sector, then class.</summary>
            [ReadOnly] public NativeArray<int2> Work;

            [NativeDisableParallelForRestriction] public NativeArray<byte> Labels;
            [NativeDisableParallelForRestriction] public NativeArray<byte> Counts;
            [NativeDisableParallelForRestriction] public NativeArray<byte> Representatives;
            public int2 Sectors;
            public int ClassCount;

            public void Execute(int index)
            {
                using var scope = RelabelMarker.Auto();
                var item = Work[index];
                var graph = new NavSectors
                {
                    Labels = Labels, Counts = Counts, Representatives = Representatives, Sectors = Sectors,
                    Classes = ClassCount,
                };
                var movementClass = Classes.Classes[item.y];
                graph.Relabel(Field, Classes.KeySlot(item.y), movementClass.Radius, item.y, item.x);
            }
        }

        [BurstCompile]
        private struct ConnectJob : IJob
        {
            public NavSectors Graph;

            public void Execute() => Graph.Connect();
        }
    }
}
