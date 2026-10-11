using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Team per faction (index = faction). Different non-neutral teams are hostile.</summary>
    public struct FactionRelations : IComponentData
    {
        public const int MaxTeams = 16;

        public FixedList32Bytes<byte> Teams;

        public readonly byte GetTeam(byte faction) => faction < Teams.Length ? Teams[faction] : (byte)0;

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
