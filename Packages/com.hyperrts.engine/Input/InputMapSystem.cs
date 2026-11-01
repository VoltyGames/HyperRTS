using Unity.Entities;
using UnityEngine.InputSystem;

namespace HyperRTS.Input
{
    /// <summary>A system that reads one input map, enabled while the system runs.</summary>
    public abstract partial class InputMapSystem : SystemBase
    {
        protected abstract InputActionMap Map { get; }

        // Not in OnCreate: without domain reload the Input System wipes action states after the world is created.
        protected override void OnStartRunning() => InputActionsProvider.Enable(Map);

        protected override void OnStopRunning() => InputActionsProvider.Disable(Map);
    }
}
