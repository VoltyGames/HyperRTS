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
            var next = SectionedBuilding.NextRepair(sections);
            return next < 0 ? 0f : rate * sections[next].MaxHealth / math.max(SectionTime, 1e-3f);
        }

        /// <summary>The building's health: its sections' total, but never less than <see cref="WreckHealth"/>.</summary>
        public static float Health(DynamicBuffer<Section> sections) =>
            math.max(WreckHealth, SectionedBuilding.Total(sections));
    }
}
