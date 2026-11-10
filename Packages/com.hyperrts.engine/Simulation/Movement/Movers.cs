using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// What every mover in a movement job shares: the crowd's index, who moves for whom, the frame's length and the
    /// handling lookups. Ground and air jobs each keep one and open a <see cref="MoveFrame"/> per unit from it.
    /// </summary>
    internal struct Movers
    {
        [ReadOnly] public SpatialIndex Index;
        public RightOfWay RightOfWay;
        [ReadOnly] public ComponentLookup<Locomotion> Locomotions;

        // Each unit writes only its own state.
        [NativeDisableParallelForRestriction] public ComponentLookup<LocomotionState> States;
        public float DeltaTime;

        public readonly MoveFrame Begin(Entity entity, in LocalTransform transform, in NavAgent agent, in Owner owner,
            in MoveDestination destination, bool moving, float top, float2 velocity)
        {
            var locomotion = Locomotions.TryGetComponent(entity, out var own) ? own : Locomotion.Instant;
            var remembers = States.TryGetComponent(entity, out var drive);
            if (!remembers)
            {
                // Without state, last frame's movement stands in for last frame's steering choice.
                drive.LastChoice = velocity;
            }

            var from = transform.Position.xz;
            var facing = math.normalizesafe(transform.Forward().xz);
            var crowd = new CrowdVisitor(entity, from, facing, velocity, agent, owner.Value, moving, destination,
                RightOfWay) { PassesInfantry = drive.PassesInfantry };
            Index.QueryHulls(crowd.Center, crowd.Reach(top), ref crowd);
            var pace = crowd.WalksForInfantry ? math.min(top, RightOfWay.WalkingPace) : top;
            return new MoveFrame
            {
                Entity = entity,
                From = from,
                Facing = facing,
                Velocity = velocity,
                WasMoving = moving,
                Remembers = remembers,
                Crowd = crowd,
                Driver = new Driver(locomotion, agent, pace, DeltaTime) { Position = from, Facing = facing, State = drive },
            };
        }

        /// <summary>Stores the handling state for a mover that keeps one.</summary>
        public void End(ref MoveFrame frame, float2 position, float2 step, bool walled)
        {
            if (!frame.Remembers)
            {
                return;
            }

            var moved = position - frame.From;
            frame.Driver.State.Hit(moved, step, walled);
            frame.Driver.State.WaitOnInfantry(frame.Crowd.IsHeldByInfantry, !frame.Crowd.IsNearInfantry,
                math.length(moved), DeltaTime);
            States[frame.Entity] = frame.Driver.State;
        }
    }
}
