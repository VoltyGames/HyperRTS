using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Transport;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>Capture-target check shared by orders and the capture behaviour.</summary>
    public struct CaptureLookup
    {
        [ReadOnly] private ComponentLookup<Capturable> _capturables;
        [ReadOnly] private ComponentLookup<Health> _health;
        [ReadOnly] private ComponentLookup<Owner> _owners;
        [ReadOnly] private BufferLookup<Cargo> _cargo;

        public CaptureLookup(ref SystemState state)
        {
            _capturables = state.GetComponentLookup<Capturable>(true);
            _health = state.GetComponentLookup<Health>(true);
            _owners = state.GetComponentLookup<Owner>(true);
            _cargo = state.GetBufferLookup<Cargo>(true);
        }

        public void Update(ref SystemState state)
        {
            _capturables.Update(ref state);
            _health.Update(ref state);
            _owners.Update(ref state);
            _cargo.Update(ref state);
        }

        /// <summary>A living, empty capturable building not allied with <paramref name="owner"/>.</summary>
        public bool CanCapture(Entity target, byte owner, in OwnerRelations relations)
        {
            if (!_capturables.HasComponent(target))
            {
                return false;
            }

            // Garrisons have to be cleared out first.
            if (_cargo.TryGetBuffer(target, out var cargo) && !cargo.IsEmpty)
            {
                return false;
            }

            return Health.IsAlive(_health, target) && !relations.IsAllied(owner, _owners[target].Value);
        }
    }
}
