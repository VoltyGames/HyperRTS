using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Adds the builder components to a unit; fill the returned buffer with building prefabs.</summary>
    public static class BuilderSetup
    {
        public static DynamicBuffer<BuildOption> Add<TSink>(ref TSink sink, float rate) where TSink : struct, IComponentSink
        {
            sink.Add(new Builder { Rate = rate });
            return sink.AddBuffer<BuildOption>();
        }
    }
}
