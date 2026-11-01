using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Destruction
{
    public partial struct SectionedBuilding
    {
        /// <summary>
        /// Rebuilds the latest broken section first, each coming back once full, then mends the standing ones.
        /// Returns the health restored.
        /// </summary>
        public float Repair(DynamicBuffer<Section> sections, float amount)
        {
            var restored = 0f;
            while (amount > 0f)
            {
                var index = NextRepair(sections);
                if (index < 0)
                {
                    break;
                }

                var section = sections[index];
                var added = math.min(section.MaxHealth - section.Health, amount);
                section.Health += added;
                amount -= added;
                restored += added;
                if (section.IsBroken && section.Health >= section.MaxHealth)
                {
                    section.BreakOrder = 0;
                    Broken &= ~(1ul << index);
                    Fell &= ~(1ul << index);
                }

                sections[index] = section;
            }

            return restored;
        }

        /// <summary>The section a heal reaches next, the latest broken one that can stand else a damaged one; -1 when all are whole.</summary>
        public static int NextRepair(DynamicBuffer<Section> sections)
        {
            var index = Latest(sections);
            return index >= 0 ? index : Damaged(sections);
        }

        /// <summary>The sections' health added up, which is the building's health.</summary>
        public static float Total(DynamicBuffer<Section> sections)
        {
            var total = 0f;
            foreach (var section in sections)
            {
                total += section.Health;
            }

            return total;
        }

        private static int Latest(DynamicBuffer<Section> sections)
        {
            // One that cannot stand yet would only fall again, so supports are rebuilt before what rests on them.
            var supported = Supported(sections);
            var latest = -1;
            for (var i = 0; i < sections.Length; i++)
            {
                var stands = sections[i].StandsOn(supported);
                if (sections[i].IsBroken && stands && (latest < 0 || sections[i].BreakOrder > sections[latest].BreakOrder))
                {
                    latest = i;
                }
            }

            return latest;
        }

        private static int Damaged(DynamicBuffer<Section> sections)
        {
            for (var i = 0; i < sections.Length; i++)
            {
                if (sections[i].Health < sections[i].MaxHealth)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
