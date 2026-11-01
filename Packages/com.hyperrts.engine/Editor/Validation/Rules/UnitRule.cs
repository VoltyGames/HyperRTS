using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Grid;
using UnityEditor;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Aircraft get their height from Flight; the Air layer alone would skim the ground.</summary>
    public sealed class UnitRule : AuthoringRule<UnitAuthoring>
    {
        protected override void Check(UnitAuthoring unit, ValidationIssues issues)
        {
            if (unit.navLayer != NavLayer.Air)
            {
                return;
            }

            if (unit.TryGetComponent<FlightAuthoring>(out _))
            {
                return;
            }

            issues.Warn(unit, "Nav layer is Air but there is no Flight component: the unit will skim the ground.",
                "Add Flight", () => Undo.AddComponent<FlightAuthoring>(unit.gameObject));
        }
    }
}
