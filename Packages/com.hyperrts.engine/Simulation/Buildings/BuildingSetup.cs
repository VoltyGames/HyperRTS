using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Vision.Fog;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Adds the building components on top of <c>GameEntitySetup</c>.</summary>
    public static class BuildingSetup
    {
        public static void Add(EntityWriter writer, in BuildingSpec spec)
        {
            writer.Add<BuildingTag>();
            if (spec.RememberedInFog)
            {
                writer.Add<RememberedInFog>();
            }

            NavSetup.AddObstacle(writer, spec.Footprint, spec.OpenFootprint);
            writer.Add(new ConstructionProgress { Value = spec.Complete ? 1f : 0f });
            writer.SetEnabled<ConstructionProgress>(!spec.Complete);
            writer.Add(new Offline { Reasons = spec.Complete ? OfflineReason.None : OfflineReason.UnderConstruction });
            writer.SetEnabled<Offline>(!spec.Complete);

            if (spec.PopulationProvided > 0)
            {
                writer.Add(new PopulationProvider { Value = spec.PopulationProvided });
            }

            if (spec.Power != 0f)
            {
                writer.Add(new PowerSupply { Amount = spec.Power });
            }
        }
    }
}
