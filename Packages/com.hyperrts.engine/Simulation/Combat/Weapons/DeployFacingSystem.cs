using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Sets a <see cref="Deployable"/> up to fire an ordered attack on a target in range. One deployed with the target
    /// outside its weapon's firing arc packs up; a packed one turns to face the target and deploys, so its arc is
    /// centred on it. Only weapons with a firing arc, and only ordered targets: the unit picks its own inside the arc.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(EngagementSystem))]
    public partial struct DeployFacingSystem : ISystem
    {
        /// <summary>Radians off the target a packed unit may be and still deploy.</summary>
        public const float FacingTolerance = 0.05f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new FaceJob
            {
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(FacingGoal))]
        [WithNone(typeof(Dead))]
        private partial struct FaceJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

            private void Execute(ref Deployable deployable, in AttackTarget attack, in ActiveOrder order,
                in DynamicBuffer<WeaponMount> mounts, in LocalTransform transform, ref FacingGoal goal,
                EnabledRefRW<FacingGoal> facing)
            {
                var weapon = mounts[0].Weapon;
                var ordered = order.Value.Type == OrderType.Attack && order.Value.Target == attack.Value;
                if (weapon.FiringArc <= 0f || !ordered || !attack.InRange)
                {
                    return;
                }

                var toTarget = math.normalizesafe((Transforms[attack.Value].Position - transform.Position).xz);
                if (deployable.Deployed && !weapon.InFiringArc(deployable.DeployedHeading, toTarget))
                {
                    deployable.PackUp();
                }
                else if (deployable.State == DeployState.Packed)
                {
                    TurnAndDeploy(ref deployable, transform, toTarget, ref goal, facing);
                }
            }

            private static void TurnAndDeploy(ref Deployable deployable, in LocalTransform transform, float2 toTarget,
                ref FacingGoal goal, EnabledRefRW<FacingGoal> facing)
            {
                if (math.dot(Rotations.Facing(transform.Rotation), toTarget) >= math.cos(FacingTolerance))
                {
                    deployable.Deploy();
                    return;
                }

                goal = new FacingGoal { Direction = toTarget };
                facing.ValueRW = true;
            }
        }
    }
}
