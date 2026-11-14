using HyperRTS.Simulation.Navigation.Agents;
using Unity.Entities;

namespace HyperRTS.Simulation.Navigation.Reach
{
    /// <summary>A unit's move as an order behaviour steers it: its goal, whether it moves and whether its last move ended by itself.</summary>
    public struct MoveControl
    {
        public RefRW<MoveDestination> Destination;
        public EnabledRefRW<MoveDestination> Moving;
        public EnabledRefRW<Arrival> Arrived;

        public MoveControl(RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> moving,
            EnabledRefRW<Arrival> arrived)
        {
            Destination = destination;
            Moving = moving;
            Arrived = arrived;
        }

        /// <summary>
        /// True once a walk-up is in reach. A stuck one forgets its stall, so the next approach walks up again from
        /// where it ended, for orders that never give up on their target.
        /// </summary>
        public readonly bool HasReached(ApproachState walk)
        {
            if (walk == ApproachState.Stuck)
            {
                Arrived.ValueRW = false;
            }

            return walk == ApproachState.Reached;
        }

        /// <summary>Ends the move where the unit stands.</summary>
        public readonly void Stop()
        {
            Moving.ValueRW = false;
            Arrived.ValueRW = false;
        }
    }
}
