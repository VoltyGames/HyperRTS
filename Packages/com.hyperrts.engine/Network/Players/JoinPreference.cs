using Unity.Entities;

namespace HyperRTS.Network.Players
{
    /// <summary>Client singleton: the slot to ask for when joining, seeded from <c>NetworkSession.PreferredFaction</c>.</summary>
    public struct JoinPreference : IComponentData
    {
        /// <summary>0 takes any free slot.</summary>
        public byte Faction;

        /// <summary>Watch the match instead of claiming a slot.</summary>
        public bool Observe;

        /// <summary>What to ask for when rejoining after the server granted <paramref name="granted"/>.</summary>
        public static JoinPreference Rejoining(LocalFaction granted) =>
            new() { Faction = granted.Value, Observe = granted.Value == 0 };
    }
}
