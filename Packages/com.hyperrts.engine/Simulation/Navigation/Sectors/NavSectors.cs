using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Sectors
{
    /// <summary>
    /// The sector graph per <see cref="MovementClass"/>: each sector's open cells split into 4-connected components,
    /// joined across borders into regions.
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

        public SectorLayout Layout;
        public int ClassCount;

        public static NavSectors Create(int classes, SectorLayout layout, Allocator allocator)
        {
            var count = layout.Total;
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
                Layout = layout,
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

        public readonly int SectorCount => Layout.Total;

        public void MarkAllDirty()
        {
            for (var i = 0; i < Dirty.Length; i++)
            {
                Dirty[i] = true;
            }
        }

        // Where one class's entry for a sector sits in the per-sector arrays.
        private readonly int ClassSector(int classIndex, int sector) => classIndex * SectorCount + sector;

        /// <summary>A cell's index inside its sector.</summary>
        public static int Local(int2 cell)
        {
            var local = cell % NavField.SectorCells;
            return local.y * NavField.SectorCells + local.x;
        }

        /// <summary>The component holding a cell across all classes and sectors, or -1 when closed or off the grid.</summary>
        public readonly int Component(int classIndex, int2 cell)
        {
            var sector = Layout.Index(cell);
            if (sector < 0)
            {
                return -1;
            }

            var label = Labels[(ClassSector(classIndex, sector)) * Area + Local(cell)];
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
            var label = component - Firsts[ClassSector(classIndex, sector)];
            return Layout.Cell(sector, Representatives[(ClassSector(classIndex, sector)) * MaxComponents + label]);
        }

        /// <summary>Labels every sector of every class and connects them, as editor tools and tests need.</summary>
        public void Build(in NavField field, in MovementClasses classes)
        {
            MarkAllDirty();
            for (var classIndex = 0; classIndex < ClassCount; classIndex++)
            {
                for (var sector = 0; sector < SectorCount; sector++)
                {
                    Relabel(field, classes, classIndex, sector);
                }
            }

            Connect();
        }
    }
}
