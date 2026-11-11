using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>Gathers the entities one aura reaches: who it affects by kind and by relation to the source's owner.</summary>
    public struct AuraCollector : ISpatialVisitor
    {
        public OwnerRelations Relations;
        public AuraTargets Affects;
        public NativeParallelMultiHashMap<Entity, AuraPresence> Hits;
        public AuraPresence Presence;

        public void Visit(in SpatialEntry entry)
        {
            if (Reaches(entry))
            {
                Hits.Add(entry.Entity, Presence);
            }
        }

        public readonly bool Reaches(in SpatialEntry entry) =>
            (Affects & Kind(entry)) != 0 && (Affects & Relation(entry.Owner)) != 0;

        private static AuraTargets Kind(in SpatialEntry entry)
        {
            if (entry.IsUnit)
            {
                return AuraTargets.Units;
            }

            return entry.IsBuilding ? AuraTargets.Buildings : AuraTargets.None;
        }

        private readonly AuraTargets Relation(byte target)
        {
            var source = Presence.SourceOwner;
            if (source == target)
            {
                return AuraTargets.Own;
            }

            if (Relations.IsAllied(source, target))
            {
                return AuraTargets.Allies;
            }

            return Relations.IsHostile(source, target) ? AuraTargets.Enemies : AuraTargets.Neutral;
        }
    }
}
