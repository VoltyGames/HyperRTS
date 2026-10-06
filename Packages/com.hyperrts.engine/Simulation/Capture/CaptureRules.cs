using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>Capture-target check shared by orders and the capture behaviour.</summary>
    public struct CaptureRules
    {
        [ReadOnly] private ComponentLookup<Capturable> _capturables;
        [ReadOnly] private ComponentLookup<Health> _health;
        [ReadOnly] private ComponentLookup<Faction> _factions;

        public CaptureRules(ref SystemState state)
        {
            _capturables = state.GetComponentLookup<Capturable>(true);
            _health = state.GetComponentLookup<Health>(true);
            _factions = state.GetComponentLookup<Faction>(true);
        }

        public void Update(ref SystemState state)
        {
            _capturables.Update(ref state);
            _health.Update(ref state);
            _factions.Update(ref state);
        }

        /// <summary>A living capturable building not allied with <paramref name="faction"/>.</summary>
        public bool CanCapture(Entity target, byte faction, in FactionRelations relations)
        {
            if (!_capturables.HasComponent(target))
            {
                return false;
            }

            var alive = _health.TryGetComponent(target, out var health) && health.Current > 0f;
            return alive && !relations.IsAllied(faction, _factions[target].Value);
        }
    }
}
