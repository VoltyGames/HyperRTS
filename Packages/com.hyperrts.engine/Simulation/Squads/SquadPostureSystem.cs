using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// Decides each squad's posture and <see cref="Pace"/> from its suppression, motion and orders, after cover has
    /// settled. Every member's posture is set here and nowhere else.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(SquadCoverSystem))]
    public partial struct SquadPostureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SquadRules>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new PostureJob
            {
                Rules = SystemAPI.GetSingleton<SquadRules>(),
                DeltaTime = SystemAPI.Time.DeltaTime,
                Orders = SystemAPI.GetComponentLookup<ActiveOrder>(true),
                Moves = SystemAPI.GetComponentLookup<MoveDestination>(true),
                Attacks = SystemAPI.GetComponentLookup<AttackTarget>(true),
                Work = SystemAPI.GetComponentLookup<SquadWork>(true),
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct PostureJob : IJobEntity
        {
            public SquadRules Rules;
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<ActiveOrder> Orders;
            [ReadOnly] public ComponentLookup<MoveDestination> Moves;
            [ReadOnly] public ComponentLookup<AttackTarget> Attacks;
            [ReadOnly] public ComponentLookup<SquadWork> Work;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;

            private void Execute(Entity entity, DynamicBuffer<SquadMember> members, ref Suppression suppression,
                ref Pace pace, in Velocity velocity)
            {
                var crewing = Deployables.TryGetComponent(entity, out var deployable) && deployable.Emplaced;
                if (Orders.HasEnabled(entity) && Orders[entity].Value.Type == OrderType.Retreat)
                {
                    suppression.Clear();
                    pace = Rules.RetreatPace;
                    SetPostures(members, Posture.Stand, false, crewing);
                    return;
                }

                suppression.Decay(Rules, DeltaTime);
                var level = Rules.Level(suppression.Value);
                var carriage = Carriage(level, SquadMember.IsMoving(velocity.Value), Attacks.HasEnabled(entity));
                if (Work.HasEnabled(entity) && level != SuppressionLevel.Pinned)
                {
                    carriage = Posture.Stand;
                }

                pace = Rules.PaceAt(level, carriage);
                SetPostures(members, carriage, level != SuppressionLevel.Pinned, crewing);
            }

            /// <summary>How the squad holds itself outside cover.</summary>
            private static Posture Carriage(SuppressionLevel level, bool moving, bool firing)
            {
                if (level == SuppressionLevel.Pinned)
                {
                    return Posture.Prone;
                }

                var crouched = moving ? level == SuppressionLevel.Suppressed : firing;
                return crouched ? Posture.Crouch : Posture.Stand;
            }

            private static void SetPostures(DynamicBuffer<SquadMember> members, Posture carriage, bool coverCrouches,
                bool crewing)
            {
                for (var i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (member.Alive)
                    {
                        var covered = coverCrouches && member.Cover != CoverKind.None;
                        var outside = covered ? member.CoverPosture : carriage;
                        member.Posture = member.Crews(crewing) ? member.CrewPosture : outside;
                        members[i] = member;
                    }
                }
            }
        }
    }
}
