using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Adds the builder components to a unit; fill the returned buffer with building prefabs.</summary>
    public static class BuilderSetup
    {
        public static DynamicBuffer<BuildOption> Add<TWriter>(ref TWriter writer, float rate)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Builder { Rate = rate });
            return writer.AddBuffer<BuildOption>();
        }
    }
}
