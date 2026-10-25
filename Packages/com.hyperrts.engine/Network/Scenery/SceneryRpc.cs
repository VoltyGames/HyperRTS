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

        /// <summary>Fills as many RPCs of <see cref="Capacity"/> as <paramref name="changes"/> needs, in order.</summary>
        public static void Batch(NativeList<SceneryChange> changes, bool witnessed, NativeList<SceneryRpc> batches)
        {
            var rpc = new SceneryRpc { Witnessed = witnessed };
            foreach (var change in changes)
            {
                rpc.Changes.Add(change);
                if (rpc.Changes.Length == Capacity)
                {
                    batches.Add(rpc);
                    rpc = new SceneryRpc { Witnessed = witnessed };
                }
            }

            if (rpc.Changes.Length > 0)
            {
                batches.Add(rpc);
            }
        }
    }
}
