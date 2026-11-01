using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Areas;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>Adds a sectioned building's components: its sections, their nav parts and floors, and the materials.</summary>
    public static class SectionsSetup
    {
        public static void Add(EntityWriter writer, IReadOnlyList<Section> sections,
            IReadOnlyList<SectionFactor> factors, Entity rubble)
        {
            var parts = ObstacleParts(sections);
            var decks = new List<NavAreaPart>(sections.Count);
            var floors = 0;
            var bridge = sections.Any(section => section.Deck);
            foreach (var section in sections)
            {
                decks.Add(new NavAreaPart { Center = section.Center.xz, Size = section.HalfSize.xz * 2f, Open = section.Deck });
                floors = math.max(floors, section.Floor + 1);
            }

            writer.AddBuffer(sections);
            writer.AddBuffer(parts);
            if (bridge)
            {
                writer.AddBuffer(decks);
            }

            writer.AddBuffer(factors);
            writer.Add(new SectionedBuilding { Rubble = rubble });
            writer.Add(new IntactFloors { Count = (byte)floors });
        }

        /// <summary>The footprint a building's sections close while they stand.</summary>
        public static List<NavObstaclePart> ObstacleParts(IReadOnlyList<Section> sections)
        {
            var parts = new List<NavObstaclePart>(sections.Count);
            foreach (var section in sections)
            {
                parts.Add(new NavObstaclePart
                {
                    Center = section.Center.xz, Size = section.HalfSize.xz * 2f, Closed = section.Closes,
                });
            }

            return parts;
        }

        /// <summary>Lets engineers rebuild the building, <paramref name="sectionTime"/> seconds a section.</summary>
        public static void AddRepairable(EntityWriter writer, float sectionTime) =>
            writer.Add(new Repairable { SectionTime = sectionTime });
    }
}
