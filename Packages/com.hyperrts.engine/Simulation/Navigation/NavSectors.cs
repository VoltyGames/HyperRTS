using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// The sector graph per <see cref="MovementClass"/>: inside each <see cref="NavField"/> sector, the cells the class
    /// finds open split into 4-connected components, each with a representative cell; components of neighbouring
    /// sectors that touch across the border share an edge, and union-find over the edges gives region ids. A dirty
    /// sector relabels alone, then the edges round it and the regions follow.
    /// </summary>
    public partial struct NavSectors
    {
        public const int Area = NavField.SectorCells * NavField.SectorCells;

        /// <summary>Most components one sector holds: a checkerboard of single open cells.</summary>
        public const int MaxComponents = Area / 2;

        // A pass marker for open cells not yet given a component.
        private const byte Unlabelled = byte.MaxValue;

        /// <summary>Per class, sector and local cell: the cell's component in its sector from 1, 0 when closed.</summary>
        public NativeArray<byte> Labels;

        /// <summary>Components per class and sector.</summary>
        public NativeArray<byte> Counts;

        /// <summary>Per class, sector and component: the local cell nearest the component's centroid.</summary>
        public NativeArray<byte> Representatives;

        /// <summary>Sectors relabelled at the next update, marked from the field's dirty sectors.</summary>
        public NativeArray<bool> Dirty;

        public int2 SectorsPerAxis;
        public int ClassCount;

        public static NavSectors Create(int classes, int2 sectors, Allocator allocator)
        {
            var count = sectors.x * sectors.y;
            var built = new NavSectors
            {
                Labels = new NativeArray<byte>(classes * count * Area, allocator),
                Counts = new NativeArray<byte>(classes * count, allocator),
                Representatives = new NativeArray<byte>(classes * count * MaxComponents, allocator),
                Dirty = new NativeArray<bool>(count, allocator),
                EdgeCounts = new NativeArray<byte>(classes * count * 2, allocator),
                Edges = new NativeArray<ushort>(classes * count * 2 * NavField.SectorCells, allocator),
                Firsts = new NativeArray<int>(classes * count + 1, allocator),
                Owners = new NativeList<int>(allocator),
                Regions = new NativeList<int>(allocator),
                SectorsPerAxis = sectors,
                ClassCount = classes,
            };
            built.MarkAllDirty();
            return built;
        }

        public void Dispose()
        {
            Labels.Dispose();
            Counts.Dispose();
            Representatives.Dispose();
            Dirty.Dispose();
            EdgeCounts.Dispose();
            Edges.Dispose();
            Firsts.Dispose();
            Owners.Dispose();
            Regions.Dispose();
        }

        public readonly int SectorCount => SectorsPerAxis.x * SectorsPerAxis.y;

        public void MarkAllDirty()
        {
            for (var i = 0; i < Dirty.Length; i++)
            {
                Dirty[i] = true;
            }
        }

        /// <summary>The sector holding a cell, or -1 off the sectors.</summary>
        public readonly int SectorOf(int2 cell)
        {
            var sector = cell / NavField.SectorCells;
            var outside = math.any(cell < 0 | sector >= SectorsPerAxis);
            return outside ? -1 : sector.y * SectorsPerAxis.x + sector.x;
        }

        public readonly int2 Origin(int sector) =>
            new int2(sector % SectorsPerAxis.x, sector / SectorsPerAxis.x) * NavField.SectorCells;

        /// <summary>A cell's index inside its sector.</summary>
        public static int Local(int2 cell)
        {
            var local = cell % NavField.SectorCells;
            return local.y * NavField.SectorCells + local.x;
        }

        public readonly int2 Cell(int sector, int local) =>
            Origin(sector) + new int2(local % NavField.SectorCells, local / NavField.SectorCells);

        /// <summary>The component holding a cell across all classes and sectors, or -1 when closed or off the grid.</summary>
        public readonly int Component(int classIndex, int2 cell)
        {
            var sector = SectorOf(cell);
            if (sector < 0)
            {
                return -1;
            }

            var label = Labels[(classIndex * SectorCount + sector) * Area + Local(cell)];
            return label == 0 ? -1 : Global(classIndex, sector, label);
        }

        /// <summary>The connected region of a cell for a class; 0 when closed or off the grid.</summary>
        public readonly int Region(int classIndex, int2 cell)
        {
            var component = Component(classIndex, cell);
            return component < 0 ? 0 : Regions[component];
        }

        /// <summary>The representative cell of a component.</summary>
        public readonly int2 Representative(int classIndex, int component)
        {
            var sector = Owners[component];
            var label = component - Firsts[classIndex * SectorCount + sector];
            return Cell(sector, Representatives[(classIndex * SectorCount + sector) * MaxComponents + label]);
        }

        /// <summary>
        /// Relabels one class over one sector from the field. Each call writes only that sector's entries of that class,
        /// so calls for different sectors or classes may run at once.
        /// </summary>
        public void Relabel(in NavField field, int slot, float radius, int classIndex, int sector)
        {
            var offset = (classIndex * SectorCount + sector) * Area;
            for (var local = 0; local < Area; local++)
            {
                var cell = Cell(sector, local);
                var open = math.all(cell < field.Size) && field.IsOpen(slot, cell, radius);
                Labels[offset + local] = open ? Unlabelled : (byte)0;
            }

            var count = 0;
            for (var local = 0; local < Area; local++)
            {
                if (Labels[offset + local] == Unlabelled)
                {
                    count++;
                    var representative = Flood(offset, local, (byte)count);
                    Representatives[(classIndex * SectorCount + sector) * MaxComponents + count - 1] = representative;
                }
            }

            Counts[classIndex * SectorCount + sector] = (byte)count;
        }

        // Labels one 4-connected component from a seed and returns its cell nearest the centroid.
        private byte Flood(int offset, int seed, byte label)
        {
            var members = new FixedList512Bytes<byte>();
            members.Add((byte)seed);
            Labels[offset + seed] = label;
            var sum = int2.zero;
            for (var read = 0; read < members.Length; read++)
            {
                var local = members[read];
                var at = new int2(local % NavField.SectorCells, local / NavField.SectorCells);
                sum += at;
                Visit(offset, at + new int2(1, 0), label, ref members);
                Visit(offset, at - new int2(1, 0), label, ref members);
                Visit(offset, at + new int2(0, 1), label, ref members);
                Visit(offset, at - new int2(0, 1), label, ref members);
            }

            return Nearest(members, (float2)sum / members.Length);
        }

        private void Visit(int offset, int2 at, byte label, ref FixedList512Bytes<byte> members)
        {
            if (math.any(at < 0 | at >= NavField.SectorCells))
            {
                return;
            }

            var local = at.y * NavField.SectorCells + at.x;
            if (Labels[offset + local] == Unlabelled)
            {
                Labels[offset + local] = label;
                members.Add((byte)local);
            }
        }

        private static byte Nearest(in FixedList512Bytes<byte> members, float2 centroid)
        {
            var best = members[0];
            var bestDistance = float.MaxValue;
            foreach (var local in members)
            {
                var at = new float2(local % NavField.SectorCells, local / NavField.SectorCells);
                var distance = math.distancesq(at, centroid);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = local;
                }
            }

            return best;
        }

        /// <summary>Labels every sector of every class and connects them, as editor tools and tests need.</summary>
        public void Build(in NavField field, in MovementClasses classes)
        {
            MarkAllDirty();
            for (var classIndex = 0; classIndex < ClassCount; classIndex++)
            {
                var slot = classes.FieldSlot(classIndex);
                var radius = classes.Classes[classIndex].Radius;
                for (var sector = 0; sector < SectorCount; sector++)
                {
                    Relabel(field, slot, radius, classIndex, sector);
                }
            }

            Connect();
        }
    }
}
