using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation.Sectors
{
    /// <summary>
    /// The sector graph per <see cref="MovementClass"/>: each sector's open cells split into 4-connected components,
    /// joined across borders into regions.
    /// </summary>
    public struct NavSectors
    {
        public const int Area = NavField.SectorCells * NavField.SectorCells;

        /// <summary>Most components one sector holds: a checkerboard of single open cells.</summary>
        public const int MaxComponents = Area / 2;

        /// <summary>Sectors relabelled at the next update, marked from the field's dirty sectors.</summary>
        public NativeArray<bool> Dirty;

        public SectorLabelling Labelling;
        public SectorLinks Links;
        public SectorLayout Layout;
        public int ClassCount;

        public static NavSectors Create(int classes, SectorLayout layout, Allocator allocator)
        {
            var count = layout.Total;
            var built = new NavSectors
            {
                Labelling = new SectorLabelling(classes, layout, allocator),
                Links = new SectorLinks(classes, layout, allocator),
                Dirty = new NativeArray<bool>(count, allocator),
                Layout = layout,
                ClassCount = classes,
            };
            built.MarkAllDirty();
            return built;
        }

        public void Dispose()
        {
            Labelling.Dispose();
            Links.Dispose();
            Dirty.Dispose();
        }

        public readonly int SectorCount => Layout.Total;

        private static readonly ProfilerMarker ConnectMarker = new("NavSectors.Connect");

        public void MarkAllDirty()
        {
            for (var i = 0; i < Dirty.Length; i++)
            {
                Dirty[i] = true;
            }
        }

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

            var label = Labelling.Labels[Layout.ClassSector(classIndex, sector) * Area + Local(cell)];
            return label == 0 ? -1 : Links.Global(classIndex, sector, label);
        }

        /// <summary>The connected region of a cell for a class; 0 when closed or off the grid.</summary>
        public readonly int Region(int classIndex, int2 cell)
        {
            var component = Component(classIndex, cell);
            return component < 0 ? 0 : Links.Regions[component];
        }

        /// <summary>The representative cell of a component.</summary>
        public readonly int2 Representative(int classIndex, int component)
        {
            var sector = Links.Owners[component];
            var label = component - Links.Firsts[Layout.ClassSector(classIndex, sector)];
            var local = Labelling.Representatives[Layout.ClassSector(classIndex, sector) * MaxComponents + label];
            return Layout.Cell(sector, local);
        }

        /// <summary>Labels every sector of every class and connects them, as editor tools and tests need.</summary>
        public void Build(in NavField field, in MovementClasses classes)
        {
            MarkAllDirty();
            for (var classIndex = 0; classIndex < ClassCount; classIndex++)
            {
                for (var sector = 0; sector < SectorCount; sector++)
                {
                    Labelling.Relabel(field, classes, classIndex, sector);
                }
            }

            Connect();
        }

        /// <summary>Rebuilds the edges and regions round the dirty sectors, then clears the dirty marks.</summary>
        public void Connect()
        {
            using var scope = ConnectMarker.Auto();
            Links.Rebuild(Labelling.Labels, Labelling.Counts, Dirty);
            for (var i = 0; i < Dirty.Length; i++)
            {
                Dirty[i] = false;
            }
        }
    }
}
