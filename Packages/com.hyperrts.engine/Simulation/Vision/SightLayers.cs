using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Entities;

namespace HyperRTS.Simulation.Vision;

/// <summary>The one rule picking an entity's <see cref="SightLayer"/>: aircraft first, then tall targets.</summary>
public static class SightLayers
{
    public static SightLayer Pick(bool airborne, bool tall)
    {
        if (airborne)
        {
            return SightLayer.Air;
        }

        return tall ? SightLayer.Tall : SightLayer.Ground;
    }

    public static bool IsAirborne(in NavAgent agent) => agent.Layer == NavLayer.Air;

    public static SightLayer Find(EntityManager entities, Entity entity)
    {
        var airborne = entities.HasComponent<NavAgent>(entity) && IsAirborne(entities.GetComponentData<NavAgent>(entity));
        return Pick(airborne, entities.HasComponent<TallTarget>(entity));
    }
}
