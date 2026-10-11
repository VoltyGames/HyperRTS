using System.Collections.Generic;
using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Adds the marks an entity's shots leave where they land.</summary>
    public static class ImpactMarkSetup
    {
        /// <summary>Unset entries are skipped; an entity left without marks writes no impact events.</summary>
        public static void Add<TWriter>(ref TWriter writer, IReadOnlyList<ImpactMark> marks)
            where TWriter : struct, IEntityWriter
        {
            var buffer = writer.AddBuffer<EntityImpactMark>();
            foreach (var mark in marks)
            {
                if (mark != null)
                {
                    buffer.Add(new EntityImpactMark { Mark = mark });
                }
            }
        }
    }
}
