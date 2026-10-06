using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Power;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Adds the building components on top of <c>GameEntitySetup</c>.</summary>
    public static class BuildingSetup
    {
        public static void Add<TSink>(ref TSink sink, float2 footprint, int populationProvided, bool complete,
            float power = 0f, PlacementSurface surface = PlacementSurface.Land) where TSink : struct, IComponentSink
        {
            sink.Add<BuildingTag>();
            NavSetup.AddObstacle(ref sink, footprint);
            if (surface != PlacementSurface.Land)
            {
                sink.Add(new BuildingPlacement { Surface = surface });
            }

            sink.Add(new ConstructionProgress { Value = complete ? 1f : 0f });
            sink.SetEnabled<ConstructionProgress>(!complete);

            if (populationProvided > 0)
            {
                sink.Add(new PopulationProvider { Value = populationProvided });
            }

            if (power != 0f)
            {
                sink.Add(new PowerSupply { Amount = power });
            }

            if (power < 0f)
            {
                sink.Add<Unpowered>();
                sink.SetEnabled<Unpowered>(false);
            }
        }
    }
}
