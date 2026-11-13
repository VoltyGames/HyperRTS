using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Reach;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Resources.Gathering
{
    /// <summary>
    /// Records the spot each gathering harvester stands at or heads for by its node or drop-off, so the gather loop
    /// gives every truck a spot of its own. A truck waiting farther off holds none.
    /// </summary>
    [BurstCompile]
    [WithNone(typeof(Dead))]
    [WithPresent(typeof(MoveDestination))]
    internal partial struct GatherClaimJob : IJobEntity
    {
        public NativeParallelMultiHashMap<Entity, ReachClaims.Claim>.ParallelWriter Spots;
        [ReadOnly] public TargetReach Reach;

        private void Execute(Entity entity, in HarvestState harvest, in ActiveOrder order,
            EnabledRefRO<ActiveOrder> busy, in MoveDestination destination, EnabledRefRO<MoveDestination> moving,
            in LocalTransform transform, in NavAgent agent)
        {
            if (!busy.ValueRO || order.Value.Type != OrderType.Gather)
            {
                return;
            }

            var target = harvest.Phase == HarvestPhase.Gathering ? order.Value.Target : harvest.DropOff;
            if (target == Entity.Null || !Reach.Exists(target))
            {
                return;
            }

            var point = moving.ValueRO ? destination.Value.xz : transform.Position.xz;
            if (!Reach.IsBy(target, point, agent))
            {
                return;
            }

            Spots.Add(target, new ReachClaims.Claim { Unit = entity, Point = point });
        }
    }
}
