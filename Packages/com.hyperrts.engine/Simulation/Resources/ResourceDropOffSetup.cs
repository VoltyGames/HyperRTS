using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Makes a building accept its owner's harvested cargo.</summary>
    public static class ResourceDropOffSetup
    {
        public static void Add<TSink>(ref TSink sink) where TSink : struct, IComponentSink => sink.Add<ResourceDropOff>();
    }
}
