using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Paths;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// What every mover in a movement job shares: the crowd's index, who moves for whom, the frame's length and the
    /// handling lookups. Ground and air jobs each keep one, open a <see cref="MoveFrame"/> per unit with
    /// <see cref="Begin"/> and close it with <see cref="Finish"/>.
    /// </summary>
    internal struct Movers
    {
        [ReadOnly] public SpatialIndex Index;
        public RightOfWay RightOfWay;
        [ReadOnly] public ComponentLookup<Locomotion> Locomotions;
        public float DeltaTime;

        /// <summary>Made in the system's OnCreate, so its lookup is made once; <see cref="Update"/> readies it each frame.</summary>
        public Movers(ref SystemState state)
        {
            Index = default;
            RightOfWay = default;
            Locomotions = state.GetComponentLookup<Locomotion>(true);
            DeltaTime = 0f;
        }

        public void Update(ref SystemState state, in RightOfWay rightOfWay, in SpatialIndex index, float deltaTime)
        {
            Locomotions.Update(ref state);
            RightOfWay = rightOfWay;
            Index = index;
            DeltaTime = deltaTime;
        }

        public readonly MoveFrame Begin(in MoverChunk chunk, int index, float top)
        {
            var entity = chunk.Entities[index];
            var agent = chunk.Agents[index];
            var velocity = chunk.Velocities[index].Value;
            var moving = chunk.Moving(index).ValueRO;
            var drive = chunk.Remembers ? chunk.Drives[index] : default;
            var locomotion = Locomotions.TryGetComponent(entity, out var own) ? own : Locomotion.Instant;
            if (!chunk.Remembers)
            {
                // Without state, last frame's movement stands in for last frame's steering choice.
                drive.LastChoice = velocity;
            }

            var entry = chunk.Entry(index, moving);
            var from = entry.Position.xz;
            var facing = entry.Facing;
            var crowd = new CrowdVisitor(entry, chunk.Destinations[index], RightOfWay)
            {
                PassesInfantry = drive.PassesInfantry,
            };
            Index.QueryHulls(crowd.Center, crowd.Reach(top, Index.IsNearVehicleLane(crowd.Center)), ref crowd);
            var pace = crowd.WalksForInfantry ? math.min(top, RightOfWay.WalkingPace) : top;
            return new MoveFrame
            {
                From = from,
                Facing = facing,
                Velocity = velocity,
                WasMoving = moving,
                Remembers = chunk.Remembers,
                Crowd = crowd,
                Driver = new Driver(locomotion, agent, pace, DeltaTime) { Position = from, Facing = facing, State = drive },
            };
        }

        /// <summary>Reports the arrival and stores the handling state for a mover that keeps one.</summary>
        public readonly void Finish(in MoverChunk chunk, int index, ref MoveFrame frame, ArrivalReason reason,
            float2 step, bool walled)
        {
            chunk.Progresses[index].Report(frame.WasMoving, chunk.Moving(index).ValueRO, reason,
                ref chunk.Arrivals[index], chunk.Arrived(index));
            if (!frame.Remembers)
            {
                return;
            }

            var moved = chunk.Transforms[index].Position.xz - frame.From;
            frame.Driver.State.Hit(moved, step, walled);
            frame.Driver.State.WaitOnInfantry(frame.Crowd.IsHeldByInfantry, !frame.Crowd.IsNearInfantry,
                math.length(moved), DeltaTime);
            var drives = chunk.Drives;
            drives[index] = frame.Driver.State;
        }
    }
}
