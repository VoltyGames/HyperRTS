using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads.Cover
{
    /// <summary>
    /// Puts the members of a halted squad in cover: each without a spot takes the best free <see cref="CoverSpot"/>
    /// near the squad and counts as covered once it stands there. A member without a spot on a concealing cell is in
    /// light cover.
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
            state.RequireForUpdate<SquadRules>();
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
                Squad = SystemAPI.GetSingleton<SquadRules>(),
                Blockers = SystemAPI.GetSingleton<SightBlockers>(),
                InsideLookup = SystemAPI.GetComponentLookup<Inside>(true),
                WorkLookup = SystemAPI.GetComponentLookup<SquadWork>(true),
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
            }.Schedule();
        }

        [BurstCompile]
        private partial struct CoverJob : IJobEntity
        {
            public CoverSpots Spots;
            public CoverRules Rules;
            public SquadRules Squad;
            [ReadOnly] public SightBlockers Blockers;
            [ReadOnly] public ComponentLookup<Inside> InsideLookup;
            [ReadOnly] public ComponentLookup<SquadWork> WorkLookup;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;

            private void Execute(Entity squad, DynamicBuffer<SquadMember> members, in LocalTransform transform,
                in Velocity velocity)
            {
                if (InsideLookup.HasEnabled(squad))
                {
                    return;
                }

                // A squad at work digs where it stands and leaves any spot.
                var halted = !SquadMember.IsMoving(velocity.Value) && !WorkLookup.HasEnabled(squad);
                var crewing = Deployable.IsEmplaced(Deployables, squad);
                Keep(squad, members, transform, halted, crewing);
                Seat(squad, members, transform, halted, crewing);
            }

            /// <summary>Gives each living member a spot when the squad has halted, and settles it into its cover.</summary>
            private void Seat(Entity squad, DynamicBuffer<SquadMember> members, in LocalTransform transform, bool halted,
                bool crewing)
            {
                var near = new NativeList<int>(0, Allocator.Temp);
                var searched = false;
                var forward = math.normalizesafe(math.rotate(transform.Rotation, math.forward()).xz);
                var living = SquadMember.CountFreeLiving(members, crewing);
                var rank = 0;
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!member.Alive)
                    {
                        continue;
                    }

                    if (member.Crews(crewing))
                    {
                        member.Cover = CoverKind.None;
                        member.Sink = 0f;
                        members[i] = member;
                        continue;
                    }

                    if (halted && !member.HasSpot)
                    {
                        if (!searched)
                        {
                            Spots.FindNear(transform.Position.xz, Rules.Reach(Squad.MaxSpread), near);
                            searched = true;
                        }

                        Take(ref member, squad, transform, forward, Squad.SpreadSlot(rank, living), near);
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
            private void Keep(Entity squad, DynamicBuffer<SquadMember> members, in LocalTransform transform, bool halted,
                bool crewing)
            {
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!member.HasSpot)
                    {
                        continue;
                    }

                    if (halted && member.Alive && !member.Crews(crewing) && Spots.TryFind(member.SpotEntity, member.SpotIndex, out var spot))
                    {
                        var reservation = Spots.Reservations[spot];
                        if (reservation.TryReserve(squad))
                        {
                            Spots.Reservations[spot] = reservation;
                            member.Slot = Spots.Spots[spot].Position.xz - transform.Position.xz;
                        }
                        else
                        {
                            member.LeaveSpot();
                        }
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
                var best = -1;
                var bestScore = float.MinValue;
                foreach (var spot in near)
                {
                    var candidate = Spots.Spots[spot];
                    if (!Spots.Reservations[spot].IsFree || !Rules.Faces(candidate.Facing, forward))
                    {
                        continue;
                    }

                    // The kind's preference outweighs distance between a spot and the member's place in the ring.
                    var score = Rules.Entry(candidate.Kind).Preference
                        - math.distance(candidate.Position.xz, transform.Position.xz + place);
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
                    member.CoverHeading = SquadMember.PackHeading(held.Facing);
                }
                else if (halted && Blockers.IsConcealing(member.Position(squad)))
                {
                    member.Cover = CoverKind.Light;
                    member.CoverHeading = SquadMember.PackHeading(forward);
                }
            }
        }

        private static Reservation Reserved(Reservation reservation, Entity holder)
        {
            reservation.Reserve(holder);
            return reservation;
        }
    }
}
