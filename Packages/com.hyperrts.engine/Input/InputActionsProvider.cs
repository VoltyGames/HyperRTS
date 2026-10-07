using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HyperRTS.Input
{
    /// <summary>
    /// The one <see cref="RTSInputActions"/> instance the input systems and the camera share. Maps are enabled per
    /// user, so one system stopping never switches off a map another still reads. Menus and rebinding
    /// <see cref="Suspend"/> every map until they <see cref="Resume"/>.
    /// </summary>
    public static class InputActionsProvider
    {
        private static readonly Dictionary<InputActionMap, int> Users = new();
        private static readonly HashSet<object> Suspenders = new();
        private static RTSInputActions _actions;

        public static RTSInputActions Actions => _actions ??= new RTSInputActions();

        public static bool IsSuspended => Suspenders.Count > 0;

        public static void Enable(InputActionMap map)
        {
            Users.TryGetValue(map, out var count);
            Users[map] = count + 1;
            if (!IsSuspended)
            {
                map.Enable();
            }
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

        /// <summary>Disables every map while <paramref name="owner"/> (an open menu, a rebind) holds input.</summary>
        public static void Suspend(object owner)
        {
            if (!Suspenders.Add(owner) || Suspenders.Count > 1)
            {
                return;
            }

            foreach (var map in Users.Keys)
            {
                map.Disable();
            }
        }

        public static void Resume(object owner)
        {
            if (!Suspenders.Remove(owner) || IsSuspended)
            {
                return;
            }

            foreach (var map in Users.Keys)
            {
                map.Enable();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Users.Clear();
            Suspenders.Clear();
            _actions?.Dispose();
            _actions = null;
        }
    }
}
