using HyperRTS.Client.Interaction;
using HyperRTS.Core;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace HyperRTS.Input.Commands
{
    /// <summary>
    /// Records the letters and digits pressed this frame into <see cref="HotkeyPresses"/> for HUD hotkeys, and turns
    /// the command card back to its first page on Escape.
    /// </summary>
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
                Collect(keyboard, Key.A, Key.Z, 'A', ref presses);
                Collect(keyboard, Key.Digit1, Key.Digit0, '1', ref presses);
            }

            SystemAPI.SetSingleton(presses);
            // Escape cancels a placement or targeted order before it turns the command card back to its first page.
            if (InputActionsProvider.CancelPressed && !placing && !HasPendingCommand())
            {
                SystemAPI.SetSingleton(new CommandCardPage());
            }
        }

        private bool HasPendingCommand() => SystemAPI.GetSingleton<PendingCommand>().Type != CommandType.None;

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
