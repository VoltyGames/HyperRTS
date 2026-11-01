using HyperRTS.Core;
using HyperRTS.Presentation.Fog;
using Unity.Entities;

namespace HyperRTS.Input.Selection
{
    /// <summary>Publishes <see cref="SightShapeView"/>: the selected units' sight is outlined while its key is held.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SightShapeInputSystem : SystemBase
    {
        private RTSInputActions _actions;

        protected override void OnCreate()
        {
            _actions = InputActionsProvider.Actions;
            EntityManager.CreateSingleton<SightShapeView>("Sight Shape View");
        }

        // Not in OnCreate: without domain reload the Input System wipes action states after the world is created.
        protected override void OnStartRunning() => InputActionsProvider.Enable(_actions.Selection);

        protected override void OnStopRunning() => InputActionsProvider.Disable(_actions.Selection);

        protected override void OnUpdate() =>
            SystemAPI.SetSingleton(new SightShapeView { Shown = _actions.Selection.SightShape.IsPressed() });
    }
}
