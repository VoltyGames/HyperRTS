using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Spatial;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Fields
{
    /// <summary>Who a field affects and how overlapping fields collapse into one presence per field id.</summary>
    public static class FieldRules
    {
        public static bool Affects(FieldTargets affects, byte sourceFaction, in SpatialEntry entry,
            in FactionRelations relations)
        {
            var kind = entry.IsUnit ? FieldTargets.Units : FieldTargets.Buildings;
            var relation = entry.Faction == sourceFaction ? FieldTargets.Own
                : relations.IsAllied(sourceFaction, entry.Faction) ? FieldTargets.Allies
                : relations.IsHostile(sourceFaction, entry.Faction) ? FieldTargets.Enemies
                : FieldTargets.Neutral;
            return (affects & kind) != 0 && (affects & relation) != 0;
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
