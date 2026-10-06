using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Makes a player entity computer-controlled; fill the returned buffer with its build order.</summary>
    public static class AIPlayerSetup
    {
        public static DynamicBuffer<AIBuildStep> Add<TWriter>(ref TWriter writer, in AIPlayer ai)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(ai);
            return writer.AddBuffer<AIBuildStep>();
        }
    }
}
