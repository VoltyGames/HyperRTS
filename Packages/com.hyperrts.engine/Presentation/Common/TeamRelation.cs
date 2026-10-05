using HyperRTS.Simulation.Match;

namespace HyperRTS.Presentation.Common
{
    /// <summary>Classifies factions relative to the local player.</summary>
    public static class TeamRelation
    {
        public static Relation Of(byte localFaction, byte faction, in FactionRelations relations)
        {
            if (faction == Faction.Neutral)
            {
                return Relation.Neutral;
            }

            if (faction == localFaction)
            {
                return Relation.Own;
            }

            if (relations.IsAllied(localFaction, faction))
            {
                return Relation.Ally;
            }

            return relations.IsHostile(localFaction, faction) ? Relation.Enemy : Relation.Neutral;
        }
    }
}
