using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Commands
{
    /// <summary>Sends the local player's commands to the server, which runs them; clients never execute them.</summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderLast = true)]
    public partial struct CommandSendSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<LocalPlayer>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
            var commands = SystemAPI.GetBuffer<PlayerCommand>(player);
            if (commands.IsEmpty)
            {
                return;
            }

            var faction = SystemAPI.GetComponent<Player>(player).Faction;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            foreach (var command in commands)
            {
                var rpc = ecb.CreateEntity();
                ecb.AddComponent(rpc, ToRpc(ref state, command, faction));
                ecb.AddComponent<SendRpcCommandRequest>(rpc);
            }

            commands.Clear();
        }

        private CommandRpc ToRpc(ref SystemState state, in PlayerCommand command, byte faction)
        {
            var rpc = new CommandRpc
            {
                Type = (byte)command.Type,
                Queue = command.Queue,
                Argument = command.Argument,
                Position = command.Position,
                Target = GhostId(ref state, command.Target),
                PrefabTypeId = SystemAPI.HasComponent<EntityInfo>(command.Prefab)
                    ? SystemAPI.GetComponent<EntityInfo>(command.Prefab).TypeId
                    : 0,
            };

            if (command.Unit != Entity.Null)
            {
                rpc.Subjects.Add(GhostId(ref state, command.Unit));
                return rpc;
            }

            foreach (var (owner, ghost) in SystemAPI.Query<RefRO<Faction>, RefRO<GhostInstance>>().WithAll<Selected>())
            {
                if (owner.ValueRO.Value == faction && rpc.Subjects.Length < CommandRpc.MaxSubjects)
                {
                    rpc.Subjects.Add(ghost.ValueRO.ghostId);
                }
            }

            return rpc;
        }

        private int GhostId(ref SystemState state, Entity entity) =>
            SystemAPI.HasComponent<GhostInstance>(entity) ? SystemAPI.GetComponent<GhostInstance>(entity).ghostId : 0;
    }
}
