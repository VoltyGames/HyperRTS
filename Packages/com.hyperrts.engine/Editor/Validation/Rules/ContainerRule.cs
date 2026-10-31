using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Transport;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>The largest allowed passenger fits in the container, and its firing ports sit under it.</summary>
    public sealed class ContainerRule : AuthoringRule<ContainerAuthoring>
    {
        protected override void Check(ContainerAuthoring container, ValidationIssues issues)
        {
            foreach (var port in container.firingPorts ?? System.Array.Empty<Transform>())
            {
                if (port == null || !port.IsChildOf(container.transform) || port == container.transform)
                {
                    issues.Warn(container, "A firing port is empty or not under the container.");
                }
            }

            if (container.maxPassengerSize > container.capacity)
            {
                issues.Warn(container, "Max passenger size exceeds capacity, so the largest passengers never fit.",
                    "Clamp", () => EditorUndo.Record(container, "Clamp Passenger Size",
                        () => container.maxPassengerSize = container.capacity));
            }
        }
    }
}
