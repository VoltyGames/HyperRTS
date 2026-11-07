using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>Who an aura affects and how overlapping auras collapse into one presence per aura id.</summary>
    public static class AuraRules
    {
        public static bool Affects(AuraTargets affects, byte sourceOwner, in SpatialEntry entry,
            in OwnerRelations relations)
        {
            return (affects & Kind(entry)) != 0 && (affects & Relation(sourceOwner, entry.Owner, relations)) != 0;
        }

        private static AuraTargets Kind(in SpatialEntry entry)
        {
            if (entry.IsUnit)
            {
                return AuraTargets.Units;
            }

            return entry.IsBuilding ? AuraTargets.Buildings : AuraTargets.None;
        }

        private static AuraTargets Relation(byte source, byte target, in OwnerRelations relations)
        {
            if (source == target)
            {
                return AuraTargets.Own;
            }

            if (relations.IsAllied(source, target))
            {
                return AuraTargets.Allies;
            }

            return relations.IsHostile(source, target) ? AuraTargets.Enemies : AuraTargets.Neutral;
        }

        /// <summary>
        /// One presence per aura id (lowest source index wins), sorted by id, so the result never depends on the
        /// order the hits were gathered in.
        /// </summary>
        public static FixedList512Bytes<AuraPresence> Resolve(in NativeParallelMultiHashMap<Entity, AuraPresence> hits,
            Entity entity)
        {
            var result = new FixedList512Bytes<AuraPresence>();
            if (!hits.TryGetFirstValue(entity, out var hit, out var iterator))
            {
                return result;
            }

            do
            {
                Merge(ref result, hit);
            } while (hits.TryGetNextValue(out hit, ref iterator));

            return result;
        }

        public static bool SameAuras(DynamicBuffer<AuraPresence> previous, in FixedList512Bytes<AuraPresence> current)
        {
            if (previous.Length != current.Length)
            {
                return false;
            }

            for (var i = 0; i < current.Length; i++)
            {
                if (previous[i].AuraId != current[i].AuraId || previous[i].Source != current[i].Source)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Merge(ref FixedList512Bytes<AuraPresence> list, in AuraPresence hit)
        {
            var index = 0;
            while (index < list.Length && list[index].AuraId < hit.AuraId)
            {
                index++;
            }

            if (index < list.Length && list[index].AuraId == hit.AuraId)
            {
                if (hit.Source.Index < list[index].Source.Index)
                {
                    list[index] = hit;
                }

                return;
            }

            if (list.Length < list.Capacity)
            {
                list.InsertRangeWithBeginEnd(index, index + 1);
                list[index] = hit;
            }
        }
    }
}
