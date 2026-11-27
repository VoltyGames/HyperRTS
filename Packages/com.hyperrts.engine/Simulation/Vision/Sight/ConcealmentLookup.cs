using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Vision.Detection;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Vision.Sight;

/// <summary>Reads what decides whether a team sees an entity: its <see cref="Concealment"/> and <see cref="SightLayer"/>.</summary>
public struct ConcealmentLookup
{
    [ReadOnly] private ComponentLookup<Stealthed> _stealthed;
    [ReadOnly] private ComponentLookup<Concealed> _concealed;
    [ReadOnly] private ComponentLookup<TallTarget> _tall;
    [ReadOnly] private ComponentLookup<NavAgent> _agents;

    public ConcealmentLookup(ref SystemState state)
    {
        _stealthed = state.GetComponentLookup<Stealthed>(true);
        _concealed = state.GetComponentLookup<Concealed>(true);
        _tall = state.GetComponentLookup<TallTarget>(true);
        _agents = state.GetComponentLookup<NavAgent>(true);
    }

    public void Update(ref SystemState state)
    {
        _stealthed.Update(ref state);
        _concealed.Update(ref state);
        _tall.Update(ref state);
        _agents.Update(ref state);
    }

    /// <summary>Stealth first, then concealment.</summary>
    public readonly Concealment Find(Entity entity)
    {
        if (_stealthed.HasEnabled(entity))
        {
            return Concealment.Stealthed;
        }

        return _concealed.HasEnabled(entity) ? Concealment.Concealed : Concealment.None;
    }

    public readonly SightLayer Layer(Entity entity)
    {
        var airborne = _agents.TryGetComponent(entity, out var agent) && SightLayers.IsAirborne(agent);
        return SightLayers.Pick(airborne, _tall.HasComponent(entity));
    }
}
