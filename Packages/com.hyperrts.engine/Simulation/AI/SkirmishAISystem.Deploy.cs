using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
        /// <summary>
        /// Deploys <see cref="Deployable"/> units that hold a target in range, and packs up deployed ones left with
        /// nothing to shoot so the next wave can take them.
        /// </summary>
        private void Deploy(ref SystemState state, in Turn turn)
        {
            foreach (var (deployable, attack, faction, unit) in SystemAPI
                         .Query<RefRO<Deployable>, RefRO<AttackTarget>, RefRO<Faction>>()
                         .WithPresent<AttackTarget>().WithNone<Dead>().WithEntityAccess())
            {
                if (faction.ValueRO.Value != turn.Faction)
                {
                    continue;
                }

                var attacking = SystemAPI.IsComponentEnabled<AttackTarget>(unit);
                var command = DeployCommand(deployable.ValueRO, attacking, attacking && attack.ValueRO.InRange);
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
