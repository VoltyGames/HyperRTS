using Unity.Collections;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace HyperRTS.Presentation.Interaction
{
    /// <summary>
    /// Client singleton: the keys pressed last frame while gameplay input was live, written by input for HUD hotkeys
    /// (command card entries), which have no input actions of their own.
    /// </summary>
    public struct HotkeyPresses : IComponentData
    {
        // Key values stay under 256, so a byte holds one.
        private FixedList32Bytes<byte> _keys;

        public int Count => _keys.Length;

        public bool IsFull => _keys.Length == _keys.Capacity;

        public Key At(int index) => (Key)_keys[index];

        public void Add(Key key) => _keys.Add((byte)key);
    }
}
