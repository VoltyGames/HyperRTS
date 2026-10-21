using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>Who a field affects and how overlapping fields collapse into one presence per field id.</summary>
    public static class FieldRules
    {
        public static bool Affects(FieldTargets affects, byte sourceFaction, in SpatialEntry entry,
            in FactionRelations relations)
        {
            return (affects & Kind(entry)) != 0 && (affects & Relation(sourceFaction, entry.Faction, relations)) != 0;
        }

        private static FieldTargets Kind(in SpatialEntry entry)
        {
            if (entry.IsUnit)
            {
                return FieldTargets.Units;
            }

            return entry.IsBuilding ? FieldTargets.Buildings : FieldTargets.None;
        }

        private static FieldTargets Relation(byte source, byte target, in FactionRelations relations)
        {
            if (source == target)
            {
                return FieldTargets.Own;
            }

            if (relations.IsAllied(source, target))
            {
                return FieldTargets.Allies;
            }

            return relations.IsHostile(source, target) ? FieldTargets.Enemies : FieldTargets.Neutral;
        }

        /// <summary>
        /// One presence per field id (lowest source index wins), sorted by id, so the result never depends on the
        /// order the hits were gathered in.
        /// </summary>
        public static FixedList512Bytes<FieldPresence> Resolve(in NativeParallelMultiHashMap<Entity, FieldPresence> hits,
            Entity entity)
        {
            var result = new FixedList512Bytes<FieldPresence>();
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

        public static bool SameFields(DynamicBuffer<FieldPresence> previous, in FixedList512Bytes<FieldPresence> current)
        {
            if (previous.Length != current.Length)
            {
                return false;
            }

            for (var i = 0; i < current.Length; i++)
            {
                if (previous[i].FieldId != current[i].FieldId || previous[i].Source != current[i].Source)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Merge(ref FixedList512Bytes<FieldPresence> list, in FieldPresence hit)
        {
            var index = 0;
            while (index < list.Length && list[index].FieldId < hit.FieldId)
            {
                index++;
            }

            if (index < list.Length && list[index].FieldId == hit.FieldId)
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
