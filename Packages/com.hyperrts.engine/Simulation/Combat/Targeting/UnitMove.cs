using HyperRTS.Simulation.Combat.Deploy;
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
        private ComponentLookup<Deployable> _deployables;
        private Entity _entity;

        public UnitMove(RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> enabled,
            ComponentLookup<Deployable> deployables, Entity entity)
        {
            _destination = destination;
            _enabled = enabled;
            _present = true;
            _deployables = deployables;
            _entity = entity;
        }

        /// <summary>Has a destination at all, packed up or not.</summary>
        public readonly bool HasDestination => _present;

        public readonly bool IsMoving => _present && _enabled.ValueRO;

        /// <summary>Has a destination to move to and isn't rooted by deploying.</summary>
        public readonly bool IsMobile => _present && !Deployable.IsImmobile(_deployables, _entity);

        public void MoveTo(float3 goal) => _destination.ValueRW.MoveTo(_enabled, goal);

        /// <summary>Moves on to <paramref name="goal"/> unless already heading within <paramref name="tolerance"/> of it.</summary>
        public void MoveTo(float3 goal, float tolerance) => _destination.ValueRW.MoveTo(_enabled, goal, tolerance);

        /// <summary>
        /// Moves to the spot <paramref name="distance"/> from <paramref name="centre"/> on the side the unit stands at
        /// <paramref name="position"/>, re-sent only once that spot moves past the engagement repath distance.
        /// </summary>
        public void StandOff(float3 centre, float3 position, float distance)
        {
            var away = math.normalizesafe(new float3(position.x - centre.x, 0f, position.z - centre.z),
                new float3(0f, 0f, -1f));
            MoveTo(centre + away * distance, EngagementSystem.RepathDistance);
        }

        public void Halt()
        {
            if (_present)
            {
                _enabled.ValueRW = false;
            }
        }
    }
}
