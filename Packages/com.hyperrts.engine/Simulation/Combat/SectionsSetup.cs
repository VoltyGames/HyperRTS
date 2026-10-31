using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Adds a sectioned building's components: its sections, their nav parts and floors, and the materials.</summary>
    public static class SectionsSetup
    {
        public static void Add<TWriter>(ref TWriter writer, IReadOnlyList<Section> sections,
            IReadOnlyList<SectionFactor> factors, Entity rubble) where TWriter : struct, IEntityWriter
        {
            var parts = new List<NavObstaclePart>(sections.Count);
            var decks = new List<NavAreaPart>(sections.Count);
            var floors = 0;
            var bridge = sections.Any(section => section.Deck);
            foreach (var section in sections)
            {
                parts.Add(new NavObstaclePart
                {
                    Center = section.Center.xz, Size = section.HalfSize.xz * 2f, Closed = section.Anchored && !bridge,
                });
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
            writer.Add(new Sections { Rubble = rubble });
            writer.Add(new IntactFloors { Count = (byte)floors });
        }

        /// <summary>Lets engineers rebuild the building, <paramref name="sectionTime"/> seconds a section.</summary>
        public static void AddRepairable<TWriter>(ref TWriter writer, float sectionTime) where TWriter : struct, IEntityWriter =>
            writer.Add(new Repairable { SectionTime = sectionTime });
    }
}
