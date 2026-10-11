using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Turns assembled <see cref="AIMapHintsData"/> into the <see cref="AIMapHints"/> singleton's data.</summary>
    public static class AIMapHintsSetup
    {
        public static void Add<TWriter>(ref TWriter writer, AIMapHintsData data)
            where TWriter : struct, IEntityWriter
        {
            writer.Add<AIMapHints>();
            writer.AddBuffer(data.Slots);
            writer.AddBuffer(data.Lanes);
            writer.AddBuffer(data.Points);
        }
    }
}
