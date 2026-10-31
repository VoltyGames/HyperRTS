using System.Collections.Generic;
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
            var floors = 0;
            foreach (var section in sections)
            {
                parts.Add(new NavObstaclePart
                {
                    Center = section.Center.xz, Size = section.HalfSize.xz * 2f, Closed = section.Anchored,
                });
                floors = math.max(floors, section.Floor + 1);
            }

            writer.AddBuffer(sections);
            writer.AddBuffer(parts);
            writer.AddBuffer(factors);
            writer.Add(new Sections { Rubble = rubble });
            writer.Add(new IntactFloors { Count = (byte)floors });
        }
    }
}
