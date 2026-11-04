using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Keeps each squad's members in order: their health sums to the squad's after stat changes, each weapon mount
    /// counts its living carriers, and members walk toward their slots, a loose wedge while the squad moves, a ring
    /// when it halts, a row along the site it builds, or the cover spot a member holds. Members walk at most three
    /// quarters of the squad's speed relative to it, so one that fell behind visibly runs to catch up. Squads inside
    /// a container stay put. Slots ignore the nav grid, so a member can stand in a wall.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct SquadMemberSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MemberJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                InsideLookup = SystemAPI.GetComponentLookup<Inside>(true),
                WorkLookup = SystemAPI.GetComponentLookup<SquadWork>(true),
            }.Schedule();
        }

        [BurstCompile]
        private partial struct MemberJob : IJobEntity
        {
            private const float CatchUp = 0.75f;
            private const float Tolerance = 1e-3f;

            // A member this close to its place on the site digs.
            private const float DigRange = 0.25f;

            public float DeltaTime;
            [ReadOnly] public ComponentLookup<Inside> InsideLookup;
            [ReadOnly] public ComponentLookup<SquadWork> WorkLookup;

            private void Execute(Entity entity, ref Health health, DynamicBuffer<SquadMember> members,
                DynamicBuffer<WeaponMount> mounts, in Velocity velocity, in MovementSpeed speed, in LocalTransform transform)
            {
                MatchHealth(ref health, members);
                CountCarriers(members, mounts);
                if (!InsideLookup.HasEnabled(entity))
                {
                    var working = WorkLookup.HasEnabled(entity);
                    var work = working ? WorkLookup[entity] : default;
                    Walk(members, velocity.Value, speed.Value, transform.Position.xz, work, working);
                }
            }

            /// <summary>Stat changes rescale the squad's health; the members follow, and a living squad's health is their sum.</summary>
            private static void MatchHealth(ref Health health, DynamicBuffer<SquadMember> members)
            {
                if (health.Current <= 0f)
                {
                    return;
                }

                var capacity = SquadMember.TotalMax(members);
                if (capacity > 0f && math.abs(capacity - health.Max) > Tolerance)
                {
                    var scale = health.Max / capacity;
                    for (var i = 0; i < members.Length; i++)
                    {
                        var member = members[i];
                        member.Health *= scale;
                        member.MaxHealth *= scale;
                        members[i] = member;
                    }
                }

                health.Current = SquadMember.Total(members);
            }

            private static void CountCarriers(DynamicBuffer<SquadMember> members, DynamicBuffer<WeaponMount> mounts)
            {
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    if (mount.Intercepts)
                    {
                        continue;
                    }

                    var carriers = 0;
                    foreach (var member in members)
                    {
                        carriers += member.Alive && member.Weapon == i ? 1 : 0;
                    }

                    mount.Carriers = (byte)carriers;
                    mounts[i] = mount;
                }
            }

            private void Walk(DynamicBuffer<SquadMember> members, float2 squadVelocity, float speed, float2 squad,
                in SquadWork work, bool working)
            {
                var living = SquadMember.CountLiving(members);
                var moving = math.lengthsq(squadVelocity) > SquadMember.MovingSpeed * SquadMember.MovingSpeed;
                var heading = math.normalizesafe(squadVelocity);
                var reach = speed * CatchUp * DeltaTime;
                var rank = 0;
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!member.Alive)
                    {
                        continue;
                    }

                    if (!member.HasSpot)
                    {
                        var slot = moving ? SquadMember.WedgeSlot(rank, living, heading) : SquadMember.SpreadSlot(rank, living);
                        var farthest = SquadMember.MaxSpread;
                        if (working)
                        {
                            slot = work.Position(rank, living, squad) - squad;
                            farthest = SquadWork.MaxSpread;
                        }

                        member.Slot = ClampSpread(slot, farthest);
                    }

                    var gap = member.Slot - member.Offset;
                    var length = math.length(gap);
                    member.Offset = length <= reach ? member.Slot : member.Offset + gap / length * reach;
                    member.Action = working && length <= DigRange ? MemberAction.Dig : MemberAction.None;
                    members[i] = member;
                    rank++;
                }
            }

            private static float2 ClampSpread(float2 slot, float farthest)
            {
                var length = math.length(slot);
                return length > farthest ? slot / length * farthest : slot;
            }
        }
    }
}
