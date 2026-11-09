using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
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
    /// Keeps each squad's members in order: sums their health into the squad's and walks them toward their slots. Slots
    /// ignore the nav grid, so a member can stand in a wall.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct SquadMemberSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SquadRules>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MemberJob
            {
                Rules = SystemAPI.GetSingleton<SquadRules>(),
                DeltaTime = SystemAPI.Time.DeltaTime,
                InsideLookup = SystemAPI.GetComponentLookup<Inside>(true),
                WorkLookup = SystemAPI.GetComponentLookup<SquadWork>(true),
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct MemberJob : IJobEntity
        {
            private const float Tolerance = 1e-3f;

            public SquadRules Rules;
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<Inside> InsideLookup;
            [ReadOnly] public ComponentLookup<SquadWork> WorkLookup;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;

            private void Execute(Entity entity, ref Health health, DynamicBuffer<SquadMember> members,
                DynamicBuffer<WeaponMount> mounts, in Velocity velocity, in MovementSpeed speed, in LocalTransform transform)
            {
                MatchHealth(ref health, members);
                ChooseWeapons(members, mounts);
                CountCarriers(members, mounts);
                if (!InsideLookup.HasEnabled(entity))
                {
                    Walk(entity, members, velocity.Value, speed.Value, transform);
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

            private static void ChooseWeapons(DynamicBuffer<SquadMember> members, DynamicBuffer<WeaponMount> mounts)
            {
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    var upgraded = member.HasUpgrade && mounts[member.UpgradedWeapon].Enabled;
                    var weapon = upgraded ? member.UpgradedWeapon : member.BaseWeapon;
                    if (member.HasUpgrade && weapon != member.Weapon)
                    {
                        member.Weapon = weapon;
                        members[i] = member;
                    }
                }
            }

            private static void CountCarriers(DynamicBuffer<SquadMember> members, DynamicBuffer<WeaponMount> mounts)
            {
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    var carriers = 0;
                    foreach (var member in members)
                    {
                        carriers += member.Alive && member.Weapon == i ? 1 : 0;
                    }

                    mount.Carriers = (byte)carriers;
                    mounts[i] = mount;
                }
            }

            private void Walk(Entity entity, DynamicBuffer<SquadMember> members, float2 squadVelocity, float speed,
                in LocalTransform transform)
            {
                var squad = transform.Position.xz;
                var working = WorkLookup.HasEnabled(entity);
                var work = working ? WorkLookup[entity] : default;
                var crewing = Deployable.IsEmplaced(Deployables, entity);
                var facing = Deployable.Heading(Deployables, entity, transform);
                var living = SquadMember.CountFreeLiving(members, crewing);
                var moving = SquadMember.IsMoving(squadVelocity);
                var heading = math.normalizesafe(squadVelocity);
                var reach = speed * Rules.CatchUp * DeltaTime;
                var rank = 0;
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (!member.Alive)
                    {
                        continue;
                    }

                    var crewed = member.Crews(crewing);
                    if (crewed)
                    {
                        member.Slot = member.CrewSlot(facing);
                    }
                    else if (!member.HasSpot)
                    {
                        var slot = moving ? Rules.WedgeSlot(rank, living, heading) : Rules.SpreadSlot(rank, living);
                        var farthest = Rules.MaxSpread;
                        if (working)
                        {
                            slot = work.Position(rank, living, squad) - squad;
                            farthest = Rules.WorkSpread;
                        }

                        member.Slot = ClampSpread(slot, farthest);
                    }

                    var gap = member.Slot - member.Offset;
                    var length = math.length(gap);
                    member.Offset = length <= reach ? member.Slot : member.Offset + gap / length * reach;
                    member.Action = working && length <= Rules.DigRange ? MemberAction.Dig : MemberAction.None;
                    members[i] = member;
                    rank += crewed ? 0 : 1;
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
