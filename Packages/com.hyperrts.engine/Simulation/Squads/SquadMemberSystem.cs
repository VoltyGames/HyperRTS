using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Keeps each squad's members in order: sums their health into the squad's and walks them toward their slots, kept
    /// on open ground and off allied vehicle hulls.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct SquadMemberSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SquadRules>();
            state.RequireForUpdate<NavGrid>();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<OwnerRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MemberJob
            {
                Rules = SystemAPI.GetSingleton<SquadRules>(),
                DeltaTime = SystemAPI.Time.DeltaTime,
                Grid = SystemAPI.GetSingleton<NavGrid>(),
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
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
            [ReadOnly] public NavGrid Grid;
            [ReadOnly] public SpatialIndex Index;
            public OwnerRelations Relations;
            [ReadOnly] public ComponentLookup<Inside> InsideLookup;
            [ReadOnly] public ComponentLookup<SquadWork> WorkLookup;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;

            private void Execute(Entity entity, ref Health health, DynamicBuffer<SquadMember> members,
                DynamicBuffer<WeaponMount> mounts, in Velocity velocity, in MovementSpeed speed, in LocalTransform transform,
                in NavAgent agent, in Owner owner)
            {
                MatchHealth(ref health, members);
                ChooseWeapons(members, mounts);
                CountCarriers(members, mounts);
                if (!InsideLookup.HasEnabled(entity))
                {
                    var ground = new SquadGround(Grid, agent, transform.Position.xz, entity, owner.Value, Relations);
                    ground.FindHulls(Index, Spread(entity, members), DeltaTime);
                    Walk(entity, members, ground, velocity.Value, speed.Value, transform);
                }
            }

            /// <summary>Stat changes rescale the squad's health; the members follow, and a living squad's health is their sum.</summary>
            private static void MatchHealth(ref Health health, DynamicBuffer<SquadMember> members)
            {
                if (health.Current <= 0f)
                {
                    return;
                }

                var capacity = new SquadRoster(members).TotalMax();
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

                health.Current = new SquadRoster(members).Total();
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

            private void Walk(Entity entity, DynamicBuffer<SquadMember> members, in SquadGround ground,
                float2 squadVelocity, float speed, in LocalTransform transform)
            {
                var crewing = Deployable.IsEmplaced(Deployables, entity);
                var living = new SquadRoster(members).CountFreeLiving(crewing);
                var working = WorkLookup.HasEnabled(entity);
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
                    // Spots, crew places and work lines are laid out off closed ground already.
                    var placed = !crewed && !member.HasSpot && !working;
                    if (crewed)
                    {
                        member.Slot = member.CrewSlot(Deployable.Heading(Deployables, entity, transform));
                    }
                    else if (!member.HasSpot)
                    {
                        var slot = FormationSlot(entity, rank, living, squadVelocity, transform.Position.xz);
                        member.Slot = placed ? ground.Place(slot) : slot;
                    }

                    var gap = member.Slot - member.Offset;
                    var length = math.length(gap);
                    member.Offset = length <= reach ? member.Slot : member.Offset + gap / length * reach;
                    // A straight walk between two clear places can still cross a wall corner or a passing hull.
                    member.Offset = placed ? ground.Place(member.Offset) : member.Offset;
                    member.Action = working && length <= Rules.DigRange ? MemberAction.Dig : MemberAction.None;
                    members[i] = member;
                    rank += crewed ? 0 : 1;
                }
            }

            // How far round the centre members may stand: the rules' spread, or further while stragglers catch up.
            private readonly float Spread(Entity entity, DynamicBuffer<SquadMember> members)
            {
                var spread = WorkLookup.HasEnabled(entity) ? Rules.WorkSpread : Rules.MaxSpread;
                foreach (var member in members)
                {
                    spread = member.Alive ? math.max(spread, math.length(member.Offset)) : spread;
                }

                return spread;
            }

            private readonly float2 FormationSlot(Entity entity, int rank, int living, float2 squadVelocity, float2 squad)
            {
                if (WorkLookup.HasEnabled(entity))
                {
                    return ClampSpread(WorkLookup[entity].Position(rank, living, squad) - squad, Rules.WorkSpread);
                }

                var heading = math.normalizesafe(squadVelocity);
                var moving = SquadMember.IsMoving(squadVelocity);
                var slot = moving ? Rules.WedgeSlot(rank, living, heading) : Rules.SpreadSlot(rank, living);
                return ClampSpread(slot, Rules.MaxSpread);
            }

            private static float2 ClampSpread(float2 slot, float farthest)
            {
                var length = math.length(slot);
                return length > farthest ? slot / length * farthest : slot;
            }
        }
    }
}
