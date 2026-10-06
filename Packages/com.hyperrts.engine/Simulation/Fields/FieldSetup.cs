using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Stats;

namespace HyperRTS.Simulation.Fields
{
    /// <summary>Adds an area field and its stat bonuses.</summary>
    public static class FieldSetup
    {
        public static void AddField<TWriter>(ref TWriter writer, in AreaField field,
            IReadOnlyList<StatModifier> bonuses) where TWriter : struct, IEntityWriter
        {
            writer.Add(field);
            var buffer = writer.AddBuffer<AreaFieldBonus>();
            foreach (var bonus in bonuses)
            {
                buffer.Add(new AreaFieldBonus { Modifier = bonus });
            }
        }
    }
}
