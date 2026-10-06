using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Who may board which container, shared by orders and boarding. Writable: use from one thread.</summary>
    public struct Boarding
    {
        private BufferLookup<Cargo> _cargo;
        private ComponentLookup<Inside> _inside;
        private ComponentLookup<PassengerStance> _stances;
        [ReadOnly] private ComponentLookup<Container> _containers;
        [ReadOnly] private ComponentLookup<Passenger> _passengers;
        [ReadOnly] private ComponentLookup<Health> _health;
        [ReadOnly] private ComponentLookup<ConstructionProgress> _sites;
        [ReadOnly] private ComponentLookup<Faction> _factions;

        /// <summary>Order resolution only checks, so it passes <paramref name="isReadOnly"/> and never boards.</summary>
        public Boarding(ref SystemState state, bool isReadOnly)
        {
            _cargo = state.GetBufferLookup<Cargo>(isReadOnly);
            _inside = state.GetComponentLookup<Inside>(isReadOnly);
            _stances = state.GetComponentLookup<PassengerStance>(isReadOnly);
            _containers = state.GetComponentLookup<Container>(true);
            _passengers = state.GetComponentLookup<Passenger>(true);
            _health = state.GetComponentLookup<Health>(true);
            _sites = state.GetComponentLookup<ConstructionProgress>(true);
            _factions = state.GetComponentLookup<Faction>(true);
        }

        public void Update(ref SystemState state)
        {
            _cargo.Update(ref state);
            _inside.Update(ref state);
            _stances.Update(ref state);
            _containers.Update(ref state);
            _passengers.Update(ref state);
            _health.Update(ref state);
            _sites.Update(ref state);
            _factions.Update(ref state);
        }

        /// <summary>A living, finished container allied with <paramref name="unit"/> with room for it.</summary>
        public bool CanBoard(Entity unit, Entity container, in FactionRelations relations)
        {
            if (unit == container || !_passengers.TryGetComponent(unit, out var passenger))
            {
                return false;
            }

            if (!_containers.TryGetComponent(container, out var data) || !data.Fits(_cargo[container], passenger.Size))
            {
                return false;
            }

            // A container riding in another takes no passengers, so two containers can't board each other.
            if (IsInside(container))
            {
                return false;
            }

            if (!Health.IsAlive(_health, container) || _sites.HasEnabled(container))
            {
                return false;
            }

            return relations.IsAllied(_factions[unit].Value, _factions[container].Value);
        }

        public bool IsInside(Entity entity) => _inside.HasEnabled(entity);

        /// <summary>Puts <paramref name="unit"/> aboard; <paramref name="stance"/> is restored when it gets out.</summary>
        public void Board(Entity unit, Entity container, Stance stance)
        {
            _cargo[container].Add(new Cargo { Unit = unit, Size = _passengers[unit].Size });
            _inside[unit] = new Inside { Container = container };
            _stances[unit] = new PassengerStance { Value = stance };
            _inside.SetComponentEnabled(unit, true);
        }
    }
}
