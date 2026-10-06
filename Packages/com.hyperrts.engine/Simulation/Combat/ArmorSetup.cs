using System.Collections.Generic;
using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Adds per-damage-type and directional armor.</summary>
    public static class ArmorSetup
    {
        public static void Add<TSink>(ref TSink sink, IReadOnlyList<ArmorModifier> modifiers)
            where TSink : struct, IComponentSink => sink.AddBuffer(modifiers);

        public static void AddFacing<TSink>(ref TSink sink, in ArmorFacing facing) where TSink : struct, IComponentSink =>
            sink.Add(facing);
    }
}
