using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.GameEntities;

namespace HyperRTS.Editor.Validation.Rules.Units;

/// <summary>An aircraft's remains fall from where it was shot down, or they would hang in the air.</summary>
public sealed class AircraftRemainsRule : AuthoringRule<FlightAuthoring>
{
    protected override void Check(FlightAuthoring flight, ValidationIssues issues)
    {
        if (!flight.TryGetComponent<GameEntityAuthoring>(out var entity) || entity.remains == null)
        {
            return;
        }

        var falls = entity.remains.TryGetComponent<DestructibleAuthoring>(out var wreck) && wreck.falls;
        if (!falls)
        {
            issues.Error(flight, $"Remains '{entity.remains.name}' don't fall: give them a Destructible with Falls set.");
        }
    }
}
