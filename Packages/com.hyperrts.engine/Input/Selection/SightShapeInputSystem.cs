using HyperRTS.Core;
using HyperRTS.Presentation.Fog;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace HyperRTS.Input.Selection
{
    /// <summary>Publishes <see cref="SightShapeView"/>: the selected units' sight is outlined while its key is held.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SightShapeInputSystem : InputMapSystem
    {
        private RTSInputActions _actions;

        protected override void OnCreate()
        {
            _actions = InputActionsProvider.Actions;
            EntityManager.CreateSingleton<SightShapeView>("Sight Shape View");
        }

        protected override InputActionMap Map => _actions.Selection;

        protected override void OnUpdate() =>
            SystemAPI.SetSingleton(new SightShapeView { Shown = _actions.Selection.SightShape.IsPressed() });
    }
}
