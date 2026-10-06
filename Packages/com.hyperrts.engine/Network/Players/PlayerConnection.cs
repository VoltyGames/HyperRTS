using Unity.Entities;

namespace HyperRTS.Network.Players
{
    /// <summary>Server only: the connection controlling this player; 0 while the slot is free or disconnected.</summary>
    public struct PlayerConnection : IComponentData
    {
        public int NetworkId;
    }
}
