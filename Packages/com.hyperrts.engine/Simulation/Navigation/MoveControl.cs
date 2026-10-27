using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>A unit's move as an order behaviour steers it: its goal, whether it moves, and how its last move ended.</summary>
    public struct MoveControl
    {
        public RefRW<MoveDestination> Destination;
        public EnabledRefRW<MoveDestination> Moving;
        public EnabledRefRW<Arrival> Arrived;

        /// <summary>Why the last move ended by itself, read while <see cref="Arrived"/>; set by callers that know it.</summary>
        public ArrivalReason Reason;

        public MoveControl(RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> moving,
            EnabledRefRW<Arrival> arrived)
        {
            Destination = destination;
            Moving = moving;
            Arrived = arrived;
            Reason = ArrivalReason.Reached;
        }

        /// <summary>The last move gave up short of its goal.</summary>
        public readonly bool GaveUp => Arrived.ValueRO && !Moving.ValueRO && Reason == ArrivalReason.Stalled;

        /// <summary>Ends the move where the unit stands.</summary>
        public readonly void Stop()
        {
            Moving.ValueRW = false;
            Arrived.ValueRW = false;
        }

        /// <summary>Points locomotion at a pose goal, rewriting it only when it changed, so pathing isn't restarted.</summary>
        public readonly void ToPose(float3 goal, float2 facing, float runIn)
        {
            ref var destination = ref Destination.ValueRW;
            var same = math.distancesq(destination.Value.xz, goal.xz) < 0.01f && math.all(destination.Facing == facing);
            if (Moving.ValueRO && same)
            {
                return;
            }

            destination = new MoveDestination { Value = goal, Facing = facing, RunIn = runIn };
            Moving.ValueRW = true;
        }
    }
}
