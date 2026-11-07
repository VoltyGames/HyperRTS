using Unity.Entities;

namespace HyperRTS.Network.Joining
{
    /// <summary>
    /// Server only, on a player ghost: the client that owns the slot. The connection holding it now is the ghost's
    /// <c>GhostOwner</c>, 0 while the slot is free or disconnected.
    /// </summary>
    public struct PlayerConnection : IComponentData
    {
        /// <summary>The owner's <see cref="JoinToken"/>, kept after a drop so only the owner gets the slot back.</summary>
        public ulong Token;
    }
}
