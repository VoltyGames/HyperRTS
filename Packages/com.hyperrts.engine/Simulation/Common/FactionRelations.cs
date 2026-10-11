using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Team per faction (index = faction). Different non-neutral teams are hostile.</summary>
    public struct FactionRelations : IComponentData
    {
        /// <summary>Fog keeps one bit per team in a <c>ushort</c> cell (bit 0, neutral, never stamps).</summary>
        public const int MaxTeams = sizeof(ushort) * 8;

        public FixedList32Bytes<byte> Teams;

        public readonly byte GetTeam(byte faction) => faction < Teams.Length ? Teams[faction] : (byte)0;

        /// <summary>Puts <paramref name="faction"/> on <paramref name="team"/>; throws for a slot or team past the limits.</summary>
        public void SetTeam(byte faction, int team)
        {
            if (faction > Faction.Max)
            {
                throw new System.ArgumentException($"Slot {faction} is past the last faction, {Faction.Max}.");
            }

            // A team past the fog's bits would silently never see anything.
            if (team < 0 || team >= MaxTeams)
            {
                throw new System.ArgumentException($"Slot {faction} has team {team}; teams go from 0 to {MaxTeams - 1}.");
            }

            while (Teams.Length <= faction)
            {
                Teams.Add(0);
            }

            Teams[faction] = (byte)team;
        }

        public readonly bool IsHostile(byte a, byte b)
        {
            var teamA = GetTeam(a);
            var teamB = GetTeam(b);
            return teamA != 0 && teamB != 0 && teamA != teamB;
        }

        public readonly bool IsAllied(byte a, byte b) => a == b || (GetTeam(a) != 0 && GetTeam(a) == GetTeam(b));

        /// <summary>How <paramref name="other"/> relates to <paramref name="local"/>, for colour-coding.</summary>
        public readonly Relation GetRelation(byte local, byte other)
        {
            if (other == Faction.Neutral)
            {
                return Relation.Neutral;
            }

            if (other == local)
            {
                return Relation.Own;
            }

            if (IsAllied(local, other))
            {
                return Relation.Ally;
            }

            return IsHostile(local, other) ? Relation.Enemy : Relation.Neutral;
        }
    }
}
