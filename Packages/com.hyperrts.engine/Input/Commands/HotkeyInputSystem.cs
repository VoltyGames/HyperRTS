using HyperRTS.Presentation.Interaction;
using HyperRTS.Core;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace HyperRTS.Input.Commands
{
    /// <summary>Records the keys pressed this frame into <see cref="HotkeyPresses"/> for the command card's hotkeys.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial class HotkeyInputSystem : SystemBase
    {
        protected override void OnCreate() => RequireForUpdate<HotkeyPresses>();

        protected override void OnUpdate()
        {
            var presses = new HotkeyPresses();
            var keyboard = Keyboard.current;
            // Menus and rebinding suspend gameplay input; hotkeys stay quiet behind them too, and while placing, whose
            // keys (rotate) would otherwise also press a card.
            var placing = SystemAPI.GetSingleton<PlacementState>().Active;
            if (keyboard != null && !InputActionsProvider.IsSuspended && !placing)
            {
                foreach (var key in keyboard.allKeys)
                {
                    if (key.wasPressedThisFrame && !presses.IsFull)
                    {
                        presses.Add(key.keyCode);
                    }
                }
            }

            SystemAPI.SetSingleton(presses);
        }
    }
}
