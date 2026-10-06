using System.Collections.Generic;
using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>Adds the veterancy components with rank thresholds (ascending) and per-rank bonuses.</summary>
    public static class VeterancySetup
    {
        // Each buffer is filled before the next is added: adding one invalidates earlier buffer handles.
        public static void Add<TSink>(ref TSink sink, IReadOnlyList<float> thresholds,
            IReadOnlyList<VeterancyBonus> bonuses) where TSink : struct, IComponentSink
        {
            sink.Add<Experience>();
            var ranks = sink.AddBuffer<VeterancyRank>();
            foreach (var threshold in thresholds)
            {
                ranks.Add(new VeterancyRank { Experience = threshold });
            }

            var bonusBuffer = sink.AddBuffer<VeterancyBonus>();
            foreach (var bonus in bonuses)
            {
                bonusBuffer.Add(bonus);
            }
        }
    }
}
