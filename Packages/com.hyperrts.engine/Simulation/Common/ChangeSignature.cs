using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// What a system last built from: an order version, an entity count and an order-independent hash sum, each
    /// compared against the last call so a system restamps only when its entities changed.
    /// </summary>
    public struct ChangeSignature
    {
        private int _order;
        private int _count;
        private ulong _sum;

        /// <summary>The count last passed to <see cref="CountChanged"/>.</summary>
        public readonly int Count => _count;

        /// <summary>True when entities with <typeparamref name="T"/> were added, removed or changed archetype since the last call.</summary>
        public bool OrderChanged<T>(ref SystemState state) where T : unmanaged, IComponentData
        {
            var order = state.EntityManager.GetComponentOrderVersion<T>();
            var changed = order != _order;
            _order = order;
            return changed;
        }

        public bool CountChanged(int count)
        {
            var changed = count != _count;
            _count = count;
            return changed;
        }

        /// <summary>The sum must not depend on entity order, since an archetype move reorders the chunks.</summary>
        public bool SumChanged(ulong sum)
        {
            var changed = sum != _sum;
            _sum = sum;
            return changed;
        }
    }
}
