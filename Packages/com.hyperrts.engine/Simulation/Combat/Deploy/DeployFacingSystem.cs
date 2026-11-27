using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Deploy
{
    /// <summary>
    /// Packs up and turns a <see cref="Deployable"/> so an ordered target or fire mission point in range sits in the centre of its weapon's firing arc.
    /// A faced order's <see cref="Order.Facing"/> becomes its <see cref="Deployable.OrderedHeading"/>, which it turns to and deploys along once idle.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TargetingSystemGroup))]
    [UpdateAfter(typeof(FireMissionSystem))]
    public partial struct DeployFacingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new FaceJob
            {
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
            }.ScheduleParallel();
            new FacePointJob().ScheduleParallel();
            new FaceHeadingJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(FacingGoal))]
        private partial struct FaceJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

            private void Execute(ref Deployable deployable, in AttackTarget attack, in ActiveOrder order,
                in DynamicBuffer<WeaponMount> mounts, in LocalTransform transform, ref FacingGoal goal,
                EnabledRefRW<FacingGoal> facing)
            {
                var weapon = new WeaponMounts(mounts).Primary.Weapon;
                var ordered = order.Value.Type == OrderType.Attack && order.Value.Target == attack.Value;
                if (weapon.FiringArc <= 0f || !ordered || !attack.InRange)
                {
                    return;
                }

                var toTarget = math.normalizesafe((Transforms[attack.Value].Position - transform.Position).xz);
                Face(ref deployable, weapon, transform, toTarget, ref goal, facing);
            }
        }

        /// <summary>The same for a fire mission's point, which a launcher with no firing arc only deploys for.</summary>
        [BurstCompile]
        [WithPresent(typeof(FacingGoal))]
        private partial struct FacePointJob : IJobEntity
        {
            private void Execute(ref Deployable deployable, in PointAim aim, in DynamicBuffer<WeaponMount> mounts,
                in LocalTransform transform, ref FacingGoal goal, EnabledRefRW<FacingGoal> facing)
            {
                var weapon = new WeaponMounts(mounts).Primary.Weapon;
                if (weapon.FiringArc <= 0f)
                {
                    deployable.Deploy();
                    return;
                }

                var toPoint = math.normalizesafe((aim.Value - transform.Position).xz);
                Face(ref deployable, weapon, transform, toPoint, ref goal, facing);
            }
        }

        [BurstCompile]
        [WithPresent(typeof(ActiveOrder), typeof(FacingGoal))]
        private partial struct FaceHeadingJob : IJobEntity
        {
            private void Execute(ref Deployable deployable, in ActiveOrder order, EnabledRefRO<ActiveOrder> busy,
                in DynamicBuffer<QueuedOrder> queue, in LocalTransform transform, ref FacingGoal goal,
                EnabledRefRW<FacingGoal> facing)
            {
                // Each order replaces the heading, so a plain move leaves none behind.
                if (busy.ValueRO)
                {
                    deployable.OrderedHeading = order.Value.Facing;
                    return;
                }

                var heading = deployable.OrderedHeading;
                var idle = queue.Length == 0 && deployable.State == DeployState.Packed;
                if (!idle || math.all(heading == 0f))
                {
                    return;
                }

                TurnAndDeploy(ref deployable, transform, heading, ref goal, facing);
            }
        }

        /// <summary>Packs up a deployed unit that points off its target, and turns a packed one onto it before it deploys.</summary>
        private static void Face(ref Deployable deployable, in Weapon weapon, in LocalTransform transform, float2 toTarget,
            ref FacingGoal goal, EnabledRefRW<FacingGoal> facing)
        {
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
            if (Planar.SameFacing(Rotations.Facing(transform.Rotation), toTarget))
            {
                deployable.Deploy();
                return;
            }

            goal = new FacingGoal { Direction = toTarget };
            facing.ValueRW = true;
        }
    }
}
