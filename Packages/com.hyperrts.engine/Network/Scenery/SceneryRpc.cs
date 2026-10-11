using Unity.Collections;
using Unity.NetCode;

namespace HyperRTS.Network.Scenery
{
    /// <summary>
    /// Scenery changes the client's team has seen. Witnessed ones happened in view and play their effects; the rest
    /// (found later, or the catch-up on joining) show only the result.
    /// </summary>
    public struct SceneryRpc : IRpcCommand
    {
        /// <summary>Most changes one message carries, so it fits a packet.</summary>
        public const int Capacity = 112;

        public bool Witnessed;

        [GhostFixedListCapacity(Capacity = Capacity)]
        public FixedList4096Bytes<SceneryChange> Changes;
    }
}
