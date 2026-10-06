using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HyperRTS.Input
{
    /// <summary>
    /// The one <see cref="RTSInputActions"/> instance the input systems and the camera share. Maps are enabled per
    /// user, so one system stopping never switches off a map another still reads.
    /// </summary>
    public static class InputActionsProvider
    {
        private static readonly Dictionary<InputActionMap, int> Users = new();
        private static RTSInputActions _actions;

        public static RTSInputActions Actions => _actions ??= new RTSInputActions();

        public static void Enable(InputActionMap map)
        {
            Users.TryGetValue(map, out var count);
            Users[map] = count + 1;
            map.Enable();
        }

        public static void Disable(InputActionMap map)
        {
            if (!Users.TryGetValue(map, out var count))
            {
                return;
            }

            if (count > 1)
            {
                Users[map] = count - 1;
                return;
            }

            Users.Remove(map);
            map.Disable();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Users.Clear();
            _actions?.Dispose();
            _actions = null;
        }
    }
}
