using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Puts the members of a halted squad in cover. Each one without a spot takes the best free
    /// <see cref="CoverSpot"/> near the squad (heavy over light, facing the way the squad does, close to its place in
    /// the ring) and walks to it; it counts as covered once it stands there. A member drops its spot when
    /// its squad moves or works, or its prop is gone. A member with no spot on a concealing cell (woods, tall crops) is in light
    /// cover from every side.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(SquadMemberSystem))]
    public partial struct SquadCoverSystem : ISystem
    {
        private const float Arrived = 0.05f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CoverSpots>();
            state.RequireForUpdate<CoverRules>();
            state.RequireForUpdate<SightBlockers>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spots = SystemAPI.GetSingletonRW<CoverSpots>().ValueRW;
            new CoverJob
            {
                Spots = spots,
                Rules = SystemAPI.GetSingleton<CoverRules>(),
                Blockers = SystemAPI.GetSingleton<SightBlockers>(),
                InsideLookup = SystemAPI.GetComponentLookup<Inside>(true),
                WorkLookup = SystemAPI.GetComponentLookup<SquadWork>(true),
            }.Schedule();
        }

        [BurstCompile]
        private partial struct CoverJob : IJobEntity
        {
            public CoverSpots Spots;
            public CoverRules Rules;
            [ReadOnly] public SightBlockers Blockers;
            [ReadOnly] public ComponentLookup<Inside> InsideLookup;
            [ReadOnly] public ComponentLookup<SquadWork> WorkLookup;

            private void Execute(Entity squad, DynamicBuffer<SquadMember> members, in LocalTransform transform,
                in Velocity velocity)
            {
                if (InsideLookup.HasEnabled(squad))
                {
                    return;
                }

                // A squad at work digs where it stands and leaves any spot.
                var halted = !IsMoving(velocity) && !WorkLookup.HasEnabled(squad);
                Keep(squad, members, transform, halted);
                var near = new NativeList<int>(16, Allocator.Temp);
                var forward = math.normalizesafe(math.rotate(transform.Rotation, math.forward()).xz);
                var living = SquadMember.CountLiving(members);
                var rank = 0;
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!member.Alive)
                    {
                        continue;
                    }

                    if (halted && !member.HasSpot)
                    {
                        Take(ref member, squad, transform, forward, SquadMember.SpreadSlot(rank, living), near);
                    }

                    Settle(ref member, transform.Position, forward, halted);
                    members[i] = member;
                    rank++;
                }
            }

            /// <summary>
            /// A member keeps its spot by reserving it again. A spot is held until a frame passes without one, so it
            /// stays taken whichever squad is handled first.
            /// </summary>
            private void Keep(Entity squad, DynamicBuffer<SquadMember> members, in LocalTransform transform, bool halted)
            {
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!member.HasSpot)
                    {
                        continue;
                    }

                    if (halted && member.Alive && Spots.TryFind(member.SpotEntity, member.SpotIndex, out var spot))
                    {
                        Spots.Reservations[spot] = Reserved(Spots.Reservations[spot], squad);
                        member.Slot = Spots.Spots[spot].Position.xz - transform.Position.xz;
                    }
                    else
                    {
                        member.LeaveSpot();
                    }

                    members[i] = member;
                }
            }

            private void Take(ref SquadMember member, Entity squad, in LocalTransform transform, float2 forward,
                float2 place, NativeList<int> near)
            {
                near.Clear();
                Spots.FindNear(transform.Position.xz, Rules.Reach, near);
                var best = -1;
                var bestScore = float.MinValue;
                foreach (var spot in near)
                {
                    var candidate = Spots.Spots[spot];
                    if (!Spots.Reservations[spot].IsFree || !Rules.Faces(candidate.Facing, forward))
                    {
                        continue;
                    }

                    // A kind outweighs any distance between a spot and the member's place in the ring.
                    var score = (byte)candidate.Kind * 10f - math.distance(candidate.Position.xz, transform.Position.xz + place);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = spot;
                    }
                }

                if (best < 0)
                {
                    return;
                }

                var taken = Spots.Spots[best];
                Spots.Reservations[best] = Reserved(Spots.Reservations[best], squad);
                member.SpotEntity = taken.Entity;
                member.SpotIndex = taken.Index;
                member.Slot = taken.Position.xz - transform.Position.xz;
            }

            /// <summary>Sets its cover from where it stands now; <see cref="SquadPostureSystem"/> sets the posture.</summary>
            private void Settle(ref SquadMember member, float3 squad, float2 forward, bool halted)
            {
                member.Cover = CoverKind.None;
                member.Sink = 0f;
                if (member.HasSpot && Spots.TryFind(member.SpotEntity, member.SpotIndex, out var spot))
                {
                    var held = Spots.Spots[spot];
                    var there = math.distance(member.Offset, member.Slot) < Arrived;
                    member.Cover = there ? held.Kind : CoverKind.None;
                    member.Sink = there ? held.Drop : 0f;
                    member.CoverHeading = SquadMember.HeadingOf(held.Facing);
                }
                else if (halted && Blockers.IsConcealing(member.Position(squad)))
                {
                    member.Cover = CoverKind.Light;
                    member.CoverHeading = SquadMember.HeadingOf(forward);
                }
            }
        }

        private static bool IsMoving(in Velocity velocity) =>
            math.lengthsq(velocity.Value) > SquadMember.MovingSpeed * SquadMember.MovingSpeed;

        private static Reservation Reserved(Reservation reservation, Entity holder)
        {
            reservation.Reserve(holder);
            return reservation;
        }
    }
}
