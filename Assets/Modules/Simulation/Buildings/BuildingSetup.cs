using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Adds the building components on top of <see cref="GameEntitySetup"/>.</summary>
    public static class BuildingSetup
    {
        public static void Add<TSink>(ref TSink sink, float2 footprint, int populationProvided, bool complete)
            where TSink : struct, IComponentSink
        {
            sink.Add<BuildingTag>();
            sink.Add(new NavObstacle { Size = footprint });
            sink.Add(new ConstructionProgress { Value = complete ? 1f : 0f });
            sink.SetEnabled<ConstructionProgress>(!complete);

            if (populationProvided > 0)
            {
                sink.Add(new PopulationProvider { Value = populationProvided });
            }
        }
    }
}
