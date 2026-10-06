using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Transport;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>The largest allowed passenger fits in the container.</summary>
    public sealed class ContainerRule : AuthoringRule<ContainerAuthoring>
    {
        protected override void Check(ContainerAuthoring container, ValidationIssues issues)
        {
            if (container.maxPassengerSize > container.capacity)
            {
                issues.Warn(container, "Max passenger size exceeds capacity, so the largest passengers never fit.",
                    "Clamp", () => EditorUndo.Record(container, "Clamp Passenger Size",
                        () => container.maxPassengerSize = container.capacity));
            }
        }
    }
}
