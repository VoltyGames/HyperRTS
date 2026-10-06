using Unity.NetCode;

namespace HyperRTS.Network.Players
{
    /// <summary>Server → client: the slot this client controls; 0 makes it an observer.</summary>
    public struct JoinAccepted : IRpcCommand
    {
        public byte Faction;
    }
}
