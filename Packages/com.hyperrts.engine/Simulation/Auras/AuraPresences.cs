using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>
    /// The auras reaching one entity, collapsed to one presence per aura id (lowest source index wins) and sorted by
    /// id, so the result never depends on the order the hits were gathered in.
    /// </summary>
    public struct AuraPresences
    {
        public FixedList512Bytes<AuraPresence> Items;

        public static AuraPresences Resolve(in NativeParallelMultiHashMap<Entity, AuraPresence> hits, Entity entity)
        {
            var result = new AuraPresences();
            if (!hits.TryGetFirstValue(entity, out var hit, out var iterator))
            {
                return result;
            }

            do
            {
                result.Merge(hit);
            } while (hits.TryGetNextValue(out hit, ref iterator));

            return result;
        }

        public static bool Matches(DynamicBuffer<AuraPresence> previous, in FixedList512Bytes<AuraPresence> current)
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

        private void Merge(in AuraPresence hit)
        {
            var index = 0;
            while (index < Items.Length && Items[index].AuraId < hit.AuraId)
            {
                index++;
            }

            if (index < Items.Length && Items[index].AuraId == hit.AuraId)
            {
                if (hit.Source.Index < Items[index].Source.Index)
                {
                    Items[index] = hit;
                }

                return;
            }

            if (Items.Length < Items.Capacity)
            {
                Items.InsertRangeWithBeginEnd(index, index + 1);
                Items[index] = hit;
            }
        }
    }
}
