using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Deploys <see cref="Deployable"/> units that hold a target in range, and packs up deployed ones left with
    /// nothing to shoot so the next wave can take them.
    /// </summary>
    internal struct DeployPlanner
    {
        private EntityQuery _query;

        public static DeployPlanner Create(ref SystemState state) => new()
        {
            _query = new EntityQueryBuilder(Allocator.Temp).WithAll<Deployable, Owner>().WithPresent<AttackTarget>()
                .WithNone<Dead>().Build(ref state),
        };

        public readonly void Plan(ref SystemState state, in AITurn turn)
        {
            var entities = state.EntityManager;
            foreach (var unit in _query.ToEntityArray(Allocator.Temp))
            {
                if (entities.GetComponentData<Owner>(unit).Value != turn.Owner)
                {
                    continue;
                }

                var attacking = entities.IsComponentEnabled<AttackTarget>(unit);
                var inRange = attacking && entities.GetComponentData<AttackTarget>(unit).InRange;
                var command = DeployCommand(entities.GetComponentData<Deployable>(unit), attacking, inRange);
                if (command != CommandType.None)
                {
                    turn.Commands.Add(new PlayerCommand { Type = command, Unit = unit });
                }
            }
        }

        /// <summary>Only a fully deployed unit packs up, so one still deploying keeps the target it is turning to.</summary>
        private static CommandType DeployCommand(in Deployable deployable, bool attacking, bool inRange)
        {
            if (inRange && deployable.State is DeployState.Packed or DeployState.Packing)
            {
                return CommandType.Deploy;
            }

            return !attacking && deployable.Deployed ? CommandType.PackUp : CommandType.None;
        }
    }
}
