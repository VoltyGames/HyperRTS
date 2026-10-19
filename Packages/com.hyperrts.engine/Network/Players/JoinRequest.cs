using Unity.NetCode;

namespace HyperRTS.Network.Players
{
    /// <summary>Client → server once the match has loaded: asks for a player slot.</summary>
    public struct JoinRequest : IRpcCommand
    {
        /// <summary>Preferred slot, so a reconnecting client gets its old one back; 0 takes any free slot.</summary>
        public byte Faction;

        /// <summary>Join as an observer: claim no slot.</summary>
        public bool Observe;

        /// <summary>The sender's <see cref="JoinToken"/>; the slot it owns comes back to it. 0 owns nothing.</summary>
        public ulong Token;
    }
}
