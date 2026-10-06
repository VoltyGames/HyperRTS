using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Lets passengers out around their container, or kills them with it. Writable lookups: use from one thread.</summary>
    public struct CargoExit
    {
        /// <summary>Exits spiral out by the golden angle so passengers don't stack.</summary>
        private const float GoldenAngle = 2.39996323f;

        private const int WalkableSearchRings = 8;

        private ComponentLookup<Inside> _inside;
        private ComponentLookup<CombatStance> _stances;
        private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private ComponentLookup<Passenger> _passengers;
        [ReadOnly] private ComponentLookup<NavAgent> _agents;
        [ReadOnly] private ComponentLookup<NavObstacle> _obstacles;
        private ComponentLookup<Health> _health;
        private ComponentLookup<LastAttacker> _attackers;

        public CargoExit(ref SystemState state)
        {
            _inside = state.GetComponentLookup<Inside>();
            _stances = state.GetComponentLookup<CombatStance>();
            _transforms = state.GetComponentLookup<LocalTransform>();
            _passengers = state.GetComponentLookup<Passenger>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _obstacles = state.GetComponentLookup<NavObstacle>(true);
            _health = state.GetComponentLookup<Health>();
            _attackers = state.GetComponentLookup<LastAttacker>();
        }

        public void Update(ref SystemState state)
        {
            _inside.Update(ref state);
            _stances.Update(ref state);
            _transforms.Update(ref state);
            _passengers.Update(ref state);
            _agents.Update(ref state);
            _obstacles.Update(ref state);
            _health.Update(ref state);
            _attackers.Update(ref state);
        }

        /// <summary>Lets out passenger <paramref name="slot"/> (or all for -1); returns the size freed.</summary>
        public int Unload(Entity container, DynamicBuffer<Cargo> cargo, int slot, bool hasGrid, in NavGrid grid)
        {
            var freed = 0;
            for (var i = cargo.Length - 1; i >= 0; i--)
            {
                if (slot >= 0 && i != slot)
                {
                    continue;
                }

                var unit = cargo[i].Unit;
                cargo.RemoveAt(i);
                if (_inside.HasComponent(unit))
                {
                    freed += _passengers[unit].Size;
                    Exit(unit, ExitPosition(container, unit, i, hasGrid, grid));
                }
            }

            return freed;
        }

        /// <summary>Kills every passenger, crediting whoever destroyed the container.</summary>
        public void KillAll(Entity container, DynamicBuffer<Cargo> cargo)
        {
            var killer = _attackers.TryGetComponent(container, out var attacker) ? attacker : default;
            foreach (var item in cargo)
            {
                if (_health.HasComponent(item.Unit))
                {
                    _health.GetRefRW(item.Unit).ValueRW.Current = 0f;
                    _attackers[item.Unit] = killer;
                }
            }

            cargo.Clear();
        }

        private void Exit(Entity unit, float3 position)
        {
            var inside = _inside[unit];
            _inside.SetComponentEnabled(unit, false);
            if (_stances.HasComponent(unit))
            {
                _stances[unit] = new CombatStance { Value = inside.Stance, Anchor = position };
            }

            var transform = _transforms[unit];
            transform.Position = position;
            _transforms[unit] = transform;
        }

        private float3 ExitPosition(Entity container, Entity unit, int slot, bool hasGrid, in NavGrid grid)
        {
            var center = _transforms[container].Position;
            var distance = EntityRadius.Of(container, _agents, _obstacles) + EntityRadius.Of(unit, _agents, _obstacles) + 0.5f;
            math.sincos(slot * GoldenAngle, out var sin, out var cos);
            var position = center + new float3(cos, 0f, sin) * distance;

            if (hasGrid && grid.TryFindNearestWalkable(grid.WorldToCell(position), WalkableSearchRings, out var cell))
            {
                position.xz = grid.CellCenter(cell).xz;
            }

            return position;
        }
    }
}
