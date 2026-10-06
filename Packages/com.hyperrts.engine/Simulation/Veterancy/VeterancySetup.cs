using System.Collections.Generic;
using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>Adds the veterancy components with rank thresholds (ascending) and per-rank bonuses.</summary>
    public static class VeterancySetup
    {
        // Each buffer is filled before the next is added: adding one invalidates earlier buffer handles.
        public static void Add<TWriter>(ref TWriter writer, IReadOnlyList<float> thresholds,
            IReadOnlyList<VeterancyBonus> bonuses) where TWriter : struct, IEntityWriter
        {
            writer.Add<Experience>();
            var ranks = writer.AddBuffer<VeterancyRank>();
            foreach (var threshold in thresholds)
            {
                ranks.Add(new VeterancyRank { Experience = threshold });
            }

            writer.AddBuffer(bonuses);
        }
    }
}
