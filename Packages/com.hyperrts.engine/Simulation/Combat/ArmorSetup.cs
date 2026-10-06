using System.Collections.Generic;
using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Adds per-damage-type and directional armor.</summary>
    public static class ArmorSetup
    {
        public static void Add<TWriter>(ref TWriter writer, IReadOnlyList<ArmorModifier> modifiers)
            where TWriter : struct, IEntityWriter => writer.AddBuffer(modifiers);

        public static void AddFacing<TWriter>(ref TWriter writer, in ArmorFacing facing)
            where TWriter : struct, IEntityWriter =>
            writer.Add(facing);
    }
}
