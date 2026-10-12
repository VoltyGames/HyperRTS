using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Caps how many of a unit or building type each player may have.</summary>
    public static class BuildLimitSetup
    {
        public static void Add<TWriter>(ref TWriter writer, int max) where TWriter : struct, IEntityWriter =>
            writer.Add(new BuildLimit { Max = max });
    }
}
