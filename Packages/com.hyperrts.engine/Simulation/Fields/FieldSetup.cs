using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Stats;

namespace HyperRTS.Simulation.Fields
{
    /// <summary>Adds an area field and its stat bonuses.</summary>
    public static class FieldSetup
    {
        public static void AddField<TSink>(ref TSink sink, in AreaField field, IReadOnlyList<StatModifier> bonuses)
            where TSink : struct, IComponentSink
        {
            sink.Add(field);
            var buffer = sink.AddBuffer<AreaFieldBonus>();
            foreach (var bonus in bonuses)
            {
                buffer.Add(new AreaFieldBonus { Modifier = bonus });
            }
        }
    }
}
