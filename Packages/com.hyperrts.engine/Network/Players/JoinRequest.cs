using Unity.NetCode;

namespace HyperRTS.Network.Players
{
    /// <summary>Client → server once the match has loaded: asks for a player slot.</summary>
    public struct JoinRequest : IRpcCommand
    {
        /// <summary>Preferred slot, so a reconnecting client gets its old one back; 0 takes any free slot.</summary>
        public byte Faction;
    }
}
