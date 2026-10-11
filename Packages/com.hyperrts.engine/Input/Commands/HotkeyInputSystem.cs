using HyperRTS.Core;
using HyperRTS.Simulation.Interaction;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace HyperRTS.Input.Commands
{
    /// <summary>Records the letters and digits pressed this frame into <see cref="HotkeyPresses"/> for HUD hotkeys.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial class HotkeyInputSystem : SystemBase
    {
        protected override void OnCreate() => RequireForUpdate<HotkeyPresses>();

        protected override void OnUpdate()
        {
            var presses = new HotkeyPresses();
            var keyboard = Keyboard.current;
            // Menus and rebinding suspend gameplay input; hotkeys stay quiet behind them too.
            if (keyboard != null && !InputActionsProvider.IsSuspended)
            {
                Collect(keyboard, Key.A, Key.Z, 'A', ref presses);
                Collect(keyboard, Key.Digit1, Key.Digit0, '1', ref presses);
            }

            SystemAPI.SetSingleton(presses);
        }

        private static void Collect(Keyboard keyboard, Key first, Key last, char firstChar, ref HotkeyPresses presses)
        {
            for (var key = first; key <= last; key++)
            {
                if (keyboard[key].wasPressedThisFrame && presses.Keys.Length < presses.Keys.Capacity)
                {
                    presses.Keys.Add(key == Key.Digit0 ? '0' : (char)(firstChar + (key - first)));
                }
            }
        }
    }
}
