using Unity.Entities;

namespace HyperRTS.Network.Players
{
    /// <summary>Client singleton: the slot to ask for when joining, seeded from <c>NetworkSession.PreferredFaction</c>.</summary>
    public struct JoinPreference : IComponentData
    {
        /// <summary>0 takes any free slot.</summary>
        public byte Faction;
    }
}
