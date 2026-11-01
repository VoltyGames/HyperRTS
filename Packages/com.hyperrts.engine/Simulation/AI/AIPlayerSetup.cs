using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Makes a player entity computer-controlled; fill the returned buffer with its build order.</summary>
    public static class AIPlayerSetup
    {
        public static DynamicBuffer<AIBuildStep> Add(EntityWriter writer, in AIPlayer ai)
        {
            writer.Add(ai);
            return writer.AddBuffer<AIBuildStep>();
        }
    }
}
