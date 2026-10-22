using Unity.Collections;
using Unity.Netcode;

namespace HyperRTS.Network.Scenery
{
    /// <summary>Scenery changes the client's team has seen. Only witnessed ones play their effects.</summary>
    public struct SceneryRpc : IRpcCommand
    {
        /// <summary>Sized to fit a packet at 10 bytes per change.</summary>
        public const int Capacity = 100;

        public bool Witnessed;

        [GhostFixedListCapacity(Capacity = Capacity)]
        public FixedList4096Bytes<SceneryChange> Changes;
    }
}
