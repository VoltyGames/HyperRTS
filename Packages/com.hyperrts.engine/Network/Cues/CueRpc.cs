using Unity.Collections;
using Unity.NetCode;

namespace HyperRTS.Network.Cues
{
    /// <summary>A batch of <c>CueEvent</c>s; the client finds each sound and rig through its own copy of the prefab.</summary>
    public struct CueRpc : IRpcCommand
    {
        /// <summary>48 events at 20 bytes keep the RPC under 1 KB on the wire.</summary>
        public const int Capacity = 48;

        [GhostFixedListCapacity(Capacity = Capacity)]
        public FixedList4096Bytes<PackedCue> Cues;
    }
}
