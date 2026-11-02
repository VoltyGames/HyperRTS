using HyperRTS.Simulation.Common;
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
        [ReadOnly] private ComponentLookup<Faction> _factions;
        [ReadOnly] private ComponentLookup<Repairable> _repairables;

        public RepairLookup(ref SystemState state)
        {
            _health = state.GetComponentLookup<Health>(true);
            _sites = state.GetComponentLookup<ConstructionProgress>(true);
            _factions = state.GetComponentLookup<Faction>(true);
            _repairables = state.GetComponentLookup<Repairable>(true);
        }

        public void Update(ref SystemState state)
        {
            _health.Update(ref state);
            _sites.Update(ref state);
            _factions.Update(ref state);
            _repairables.Update(ref state);
        }

        /// <summary>
        /// A damaged, living building to repair: a finished one allied with <paramref name="faction"/>, or a
        /// <see cref="Repairable"/> one, which belongs to nobody and anyone's builders mend.
        /// </summary>
        public readonly bool NeedsRepair(Entity target, byte faction, in FactionRelations relations)
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
            return finished && _factions.TryGetComponent(target, out var owner) && relations.IsAllied(faction, owner.Value);
        }
    }
}
