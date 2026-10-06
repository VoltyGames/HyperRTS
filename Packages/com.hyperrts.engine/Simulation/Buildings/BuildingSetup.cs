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
        public static void Add<TWriter>(ref TWriter writer, float2 footprint, int populationProvided, bool complete,
            float power = 0f, PlacementSurface surface = PlacementSurface.Land) where TWriter : struct, IEntityWriter
        {
            writer.Add<BuildingTag>();
            NavSetup.AddObstacle(ref writer, footprint);
            if (surface != PlacementSurface.Land)
            {
                writer.Add(new BuildingPlacement { Surface = surface });
            }

            writer.Add(new ConstructionProgress { Value = complete ? 1f : 0f });
            writer.SetEnabled<ConstructionProgress>(!complete);

            if (populationProvided > 0)
            {
                writer.Add(new PopulationProvider { Value = populationProvided });
            }

            if (power != 0f)
            {
                writer.Add(new PowerSupply { Amount = power });
            }

            if (power < 0f)
            {
                writer.Add<Unpowered>();
                writer.SetEnabled<Unpowered>(false);
            }
        }
    }
}
