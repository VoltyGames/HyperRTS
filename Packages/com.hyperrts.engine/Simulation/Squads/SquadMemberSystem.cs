using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Keeps each squad's members in order: their health sums to the squad's after stat changes, each weapon mount
    /// counts its living carriers, and members walk toward their slots, a loose wedge while the squad moves and a ring
    /// when it halts. Members walk at most three quarters of the squad's speed relative to it, so one that fell behind
    /// visibly runs to catch up. Squads inside a container stay put. Slots ignore the nav grid, so a member can stand
    /// in a wall.
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
            }.Schedule();
        }

        [BurstCompile]
        private partial struct MemberJob : IJobEntity
        {
            private const float MovingSpeed = 0.5f;
            private const float CatchUp = 0.75f;
            private const float Tolerance = 1e-3f;

            public float DeltaTime;
            [ReadOnly] public ComponentLookup<Inside> InsideLookup;

            private void Execute(Entity entity, ref Health health, DynamicBuffer<SquadMember> members,
                DynamicBuffer<WeaponMount> mounts, in Velocity velocity, in MovementSpeed speed)
            {
                MatchHealth(ref health, members);
                CountCarriers(members, mounts);
                if (!InsideLookup.HasEnabled(entity))
                {
                    Walk(members, velocity.Value, speed.Value);
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

            private void Walk(DynamicBuffer<SquadMember> members, float2 squadVelocity, float speed)
            {
                var living = SquadMember.CountLiving(members);
                var moving = math.lengthsq(squadVelocity) > MovingSpeed * MovingSpeed;
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

                    var slot = moving ? SquadMember.WedgeSlot(rank, living, heading) : SquadMember.SpreadSlot(rank, living);
                    member.Slot = ClampSpread(slot);
                    var gap = member.Slot - member.Offset;
                    var length = math.length(gap);
                    member.Offset = length <= reach ? member.Slot : member.Offset + gap / length * reach;
                    members[i] = member;
                    rank++;
                }
            }

            private static float2 ClampSpread(float2 slot)
            {
                var length = math.length(slot);
                return length > SquadMember.MaxSpread ? slot / length * SquadMember.MaxSpread : slot;
            }
        }
    }
}
