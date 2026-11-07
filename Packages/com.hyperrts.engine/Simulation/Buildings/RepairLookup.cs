using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Destruction;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Repair-target check shared by orders and the repair behaviour.</summary>
    public struct RepairLookup
    {
        [ReadOnly] private ComponentLookup<Health> _health;
        [ReadOnly] private ComponentLookup<ConstructionProgress> _sites;
        [ReadOnly] private ComponentLookup<Owner> _owners;
        [ReadOnly] private ComponentLookup<Repairable> _repairables;

        public RepairLookup(ref SystemState state)
        {
            _health = state.GetComponentLookup<Health>(true);
            _sites = state.GetComponentLookup<ConstructionProgress>(true);
            _owners = state.GetComponentLookup<Owner>(true);
            _repairables = state.GetComponentLookup<Repairable>(true);
        }

        public void Update(ref SystemState state)
        {
            _health.Update(ref state);
            _sites.Update(ref state);
            _owners.Update(ref state);
            _repairables.Update(ref state);
        }

        /// <summary>
        /// A damaged, living building to repair: a finished one allied with <paramref name="owner"/>, or a
        /// <see cref="Repairable"/> one, which belongs to nobody and anyone's builders mend.
        /// </summary>
        public readonly bool NeedsRepair(Entity target, byte owner, in OwnerRelations relations)
        {
            if (!Health.IsAlive(_health, target) || _health[target].Current >= _health[target].Max)
            {
                return false;
            }

            if (_repairables.HasComponent(target))
            {
                return true;
            }

            // Sites are built, not repaired.
            var finished = _sites.HasComponent(target) && !_sites.IsComponentEnabled(target);
            return finished && _owners.TryGetComponent(target, out var targetOwner) && relations.IsAllied(owner, targetOwner.Value);
        }
    }
}
