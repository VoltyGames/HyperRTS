using Unity.Entities;

namespace HyperRTS.Network.Players
{
    /// <summary>Server only: the connection controlling this player and the client that owns the slot.</summary>
    public struct PlayerConnection : IComponentData
    {
        /// <summary>0 while the slot is free or disconnected.</summary>
        public int NetworkId;

        /// <summary>The owner's <see cref="JoinToken"/>, kept after a drop so only the owner gets the slot back.</summary>
        public ulong Token;
    }
}
