using HyperRTS.Simulation.Navigation.Agents;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>Sends, halts and checks one unit's own <see cref="MoveDestination"/> from a combat job; a unit without one never moves.</summary>
    public struct UnitMove
    {
        private RefRW<MoveDestination> _destination;
        private EnabledRefRW<MoveDestination> _enabled;
        private bool _present;
        private bool _rooted;

        public UnitMove(RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> enabled, bool rooted)
        {
            _destination = destination;
            _enabled = enabled;
            _present = true;
            _rooted = rooted;
        }

        public readonly bool IsMoving => _present && _enabled.ValueRO;

        /// <summary>Has a destination to move to and isn't rooted by deploying.</summary>
        public readonly bool IsMobile => _present && !_rooted;

        public void MoveTo(float3 goal) => _destination.ValueRW.MoveTo(_enabled, goal);

        /// <summary>Moves on to <paramref name="goal"/> unless already heading within <paramref name="tolerance"/> of it.</summary>
        public void MoveTo(float3 goal, float tolerance) => _destination.ValueRW.MoveTo(_enabled, goal, tolerance);

        public void Halt()
        {
            if (_present)
            {
                _enabled.ValueRW = false;
            }
        }
    }
}
