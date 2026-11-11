using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>
    /// A sectioned building anyone's builders rebuild for free, one section after another, even when every section
    /// is down: it never dies, it stays as a wreck with <see cref="WreckHealth"/>.
    /// </summary>
    public struct Repairable : IComponentData
    {
        /// <summary>Health a building with every section broken keeps, so it is not destroyed.</summary>
        public const float WreckHealth = 1f;

        /// <summary>Seconds a builder of rate 1 takes to rebuild one section.</summary>
        public float SectionTime;

        /// <summary>Health per second a builder of <paramref name="rate"/> restores to the section a heal reaches next.</summary>
        public readonly float PerSecond(float rate, DynamicBuffer<Section> sections)
        {
            var next = NextRepair(sections);
            return next < 0 ? 0f : rate * sections[next].MaxHealth / math.max(SectionTime, 1e-3f);
        }

        /// <summary>The building's health: its sections' total, but never less than <see cref="WreckHealth"/>.</summary>
        public static float Health(DynamicBuffer<Section> sections) =>
            math.max(WreckHealth, Total(sections));

        /// <summary>The section a heal reaches next, the latest broken one that can stand else a damaged one; -1 when all are whole.</summary>
        public static int NextRepair(DynamicBuffer<Section> sections)
        {
            var index = Latest(sections);
            return index >= 0 ? index : Damaged(sections);
        }

        /// <summary>The sections' health added up, which is the building's health.</summary>
        private static float Total(DynamicBuffer<Section> sections)
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
            var supported = SectionedBuilding.Supported(sections);
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
