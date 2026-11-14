using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// Runs <see cref="OrderType.FireMission"/> orders with <see cref="MissionRunner"/>: moves the unit until the
    /// ordered ground point lies within its primary weapon's range, then points <see cref="PointAim"/> at it so the
    /// unit deploys facing it and fires one salvo, and ends the order once that salvo has fired.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(EngagementSystem))]
    [UpdateBefore(typeof(DeployFacingSystem))]
    public partial struct FireMissionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MobileJob { Deployables = SystemAPI.GetComponentLookup<Deployable>(true) }.ScheduleParallel();
            new FixedJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(ActiveOrder), typeof(PointAim), typeof(MoveDestination))]
        [WithNone(typeof(Dead))]
        private partial struct MobileJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<Deployable> Deployables;

            private void Execute(Entity entity, in LocalTransform transform, in DynamicBuffer<WeaponMount> mounts,
                ref ActiveOrder order, EnabledRefRW<ActiveOrder> busy, RefRW<PointAim> aim,
                EnabledRefRW<PointAim> aiming, RefRW<MoveDestination> destination, EnabledRefRW<MoveDestination> moving)
            {
                var moves = new UnitMove(destination, moving, Deployables, entity);
                new MissionRunner(busy, aim, aiming, moves).Run(transform.Position, mounts[0], order.Value);
            }
        }

        /// <summary>Launchers with no <see cref="MoveDestination"/> fire only at what is already in range.</summary>
        [BurstCompile]
        [WithPresent(typeof(ActiveOrder), typeof(PointAim))]
        [WithAbsent(typeof(MoveDestination))]
        [WithNone(typeof(Dead))]
        private partial struct FixedJob : IJobEntity
        {
            private void Execute(in LocalTransform transform, in DynamicBuffer<WeaponMount> mounts, ref ActiveOrder order,
                EnabledRefRW<ActiveOrder> busy, RefRW<PointAim> aim, EnabledRefRW<PointAim> aiming) =>
                new MissionRunner(busy, aim, aiming, default).Run(transform.Position, mounts[0], order.Value);
        }
    }
}
