using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common.Players
{
    /// <summary>Team per owner (index = owner). Different non-neutral teams are hostile.</summary>
    public struct OwnerRelations : IComponentData
    {
        /// <summary>Fog keeps one bit per team in a <c>ushort</c> cell (bit 0, neutral, never stamps).</summary>
        public const int MaxTeams = sizeof(ushort) * 8;

        public FixedList32Bytes<byte> Teams;

        public readonly byte GetTeam(byte owner) => owner < Teams.Length ? Teams[owner] : (byte)0;

        /// <summary>Puts <paramref name="owner"/> on <paramref name="team"/>; throws for a slot or team past the limits.</summary>
        public void SetTeam(byte owner, int team)
        {
            if (owner > Owner.Max)
            {
                throw new System.ArgumentException($"Slot {owner} is past the last owner, {Owner.Max}.");
            }

            // A team past the fog's bits would silently never see anything.
            if (team < 0 || team >= MaxTeams)
            {
                throw new System.ArgumentException($"Slot {owner} has team {team}; teams go from 0 to {MaxTeams - 1}.");
            }

            while (Teams.Length <= owner)
            {
                Teams.Add(0);
            }

            Teams[owner] = (byte)team;
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
            if (other == Owner.Neutral)
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
