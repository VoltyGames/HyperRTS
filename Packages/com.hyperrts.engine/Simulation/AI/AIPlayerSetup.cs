using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Makes a player entity computer-controlled; fill the returned buffer with its build order.</summary>
    public static class AIPlayerSetup
    {
        public static DynamicBuffer<AIBuildStep> Add<TSink>(ref TSink sink, in AIPlayer ai)
            where TSink : struct, IComponentSink
        {
            sink.Add(ai);
            return sink.AddBuffer<AIBuildStep>();
        }
    }
}
