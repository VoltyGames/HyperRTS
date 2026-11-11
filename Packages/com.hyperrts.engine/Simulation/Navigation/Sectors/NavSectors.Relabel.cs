using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Sectors
{
    public partial struct NavSectors
    {
        /// <summary>Bytes <see cref="Label"/> writes per class and sector: labels, representatives, then the count.</summary>
        public const int LabelledBytes = Area + MaxComponents + 1;

        /// <summary>Relabels one class over one sector from the field and stores it. Not for parallel jobs.</summary>
        public void Relabel(in NavField field, in MovementClasses classes, int classIndex, int sector)
        {
            var labelled = new FixedList512Bytes<byte>();
            Label(field, classes, classIndex, sector, ref labelled);
            Store(classIndex, sector, labelled);
        }

        /// <summary>
        /// Labels one class over one sector into <paramref name="labelled"/>, <see cref="LabelledBytes"/> of them, for
        /// <see cref="Store"/>, so parallel jobs write only their own block.
        /// </summary>
        public readonly void Label(in NavField field, in MovementClasses classes, int classIndex, int sector,
            ref FixedList512Bytes<byte> labelled)
        {
            labelled.Length = LabelledBytes;
            var slot = classes.FieldSlot(classIndex);
            var radius = classes.Classes[classIndex].Radius;
            for (var local = 0; local < Area; local++)
            {
                var cell = Layout.Cell(sector, local);
                var open = math.all(cell < field.Grid.Size) && field.IsOpen(slot, cell, radius);
                labelled[local] = open ? Unlabelled : (byte)0;
            }

            byte count = 0;
            for (var local = 0; local < Area; local++)
            {
                if (labelled[local] == Unlabelled)
                {
                    count++;
                    labelled[Area + count - 1] = Flood(ref labelled, local, count);
                }
            }

            labelled[LabelledBytes - 1] = count;
        }

        /// <summary>Stores what <see cref="Label"/> wrote for one class and sector.</summary>
        public void Store(int classIndex, int sector, in FixedList512Bytes<byte> labelled)
        {
            var key = ClassSector(classIndex, sector);
            var count = labelled[LabelledBytes - 1];
            for (var i = 0; i < Area; i++)
            {
                Labels[key * Area + i] = labelled[i];
            }

            for (var i = 0; i < count; i++)
            {
                Representatives[key * MaxComponents + i] = labelled[Area + i];
            }

            Counts[key] = count;
        }

        // Labels one 4-connected component from a seed and returns its cell nearest the centroid.
        private static byte Flood(ref FixedList512Bytes<byte> labelled, int seed, byte label)
        {
            var members = new FixedList512Bytes<byte>();
            members.Add((byte)seed);
            labelled[seed] = label;
            var sum = int2.zero;
            for (var read = 0; read < members.Length; read++)
            {
                var local = members[read];
                var at = new int2(local % NavField.SectorCells, local / NavField.SectorCells);
                sum += at;
                Visit(ref labelled, at + new int2(1, 0), label, ref members);
                Visit(ref labelled, at - new int2(1, 0), label, ref members);
                Visit(ref labelled, at + new int2(0, 1), label, ref members);
                Visit(ref labelled, at - new int2(0, 1), label, ref members);
            }

            return Nearest(members, (float2)sum / members.Length);
        }

        private static void Visit(ref FixedList512Bytes<byte> labelled, int2 at, byte label,
            ref FixedList512Bytes<byte> members)
        {
            if (math.any(at < 0 | at >= NavField.SectorCells))
            {
                return;
            }

            var local = at.y * NavField.SectorCells + at.x;
            if (labelled[local] == Unlabelled)
            {
                labelled[local] = label;
                members.Add((byte)local);
            }
        }

        private static byte Nearest(in FixedList512Bytes<byte> members, float2 centroid)
        {
            var nearest = Closest.None;
            for (var i = 0; i < members.Length; i++)
            {
                var local = members[i];
                var at = new float2(local % NavField.SectorCells, local / NavField.SectorCells);
                nearest.Consider(i, Entity.Null, math.distancesq(at, centroid));
            }

            return members[nearest.Index];
        }
    }
}
