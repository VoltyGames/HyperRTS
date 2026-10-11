using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Client.Interaction
{
    /// <summary>
    /// Client singleton: the letters and digits pressed last frame while gameplay input was live, written by input for
    /// HUD hotkeys (command card entries), which have no input actions of their own.
    /// </summary>
    public struct HotkeyPresses : IComponentData
    {
        public FixedList32Bytes<char> Keys;

        public bool Contains(char key) => Keys.Contains(char.ToUpperInvariant(key));
    }
}
