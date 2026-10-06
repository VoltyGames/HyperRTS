using Unity.Entities;

namespace HyperRTS.Network.Players
{
    /// <summary>Client singleton: the slot the server gave this client; 0 for an observer.</summary>
    public struct LocalFaction : IComponentData
    {
        public byte Value;
    }
}
