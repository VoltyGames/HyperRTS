using HyperRTS.Simulation.Common.Players;
using Unity.Entities;

namespace HyperRTS.Network.Players
{
    /// <summary>Client singleton: the slot to ask for when joining, seeded from <c>NetworkSession.Preference</c>.</summary>
    public struct JoinPreference : IComponentData
    {
        /// <summary>0 takes any free slot.</summary>
        public byte Faction;

        /// <summary>Watch the match instead of claiming a slot.</summary>
        public bool Observe;

        /// <summary>What to ask for when rejoining the seat <paramref name="granted"/>, 0 for an observer.</summary>
        public static JoinPreference Rejoining(byte granted) =>
            new() { Faction = granted, Observe = granted == 0 };
    }
}
