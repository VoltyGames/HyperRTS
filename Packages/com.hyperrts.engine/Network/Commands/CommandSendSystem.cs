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
        private EntityQuery _selected;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _selected = SystemAPI.QueryBuilder().WithAll<Selected, Faction, GhostInstance>().Build();
            state.RequireForUpdate<LocalPlayer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
            var commands = SystemAPI.GetBuffer<PlayerCommand>(player);
            var listed = SystemAPI.GetBuffer<PlayerCommandSubject>(player);
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
                ecb.AddComponent(rpc, ToRpc(ref state, command, listed, faction));
                ecb.AddComponent<SendRpcCommandRequest>(rpc);
            }

            commands.Clear();
            listed.Clear();
        }

        private CommandRpc ToRpc(ref SystemState state, in PlayerCommand command,
            DynamicBuffer<PlayerCommandSubject> listed, byte faction)
        {
            var rpc = new CommandRpc
            {
                Type = (byte)command.Type,
                Queue = command.Queue,
                Argument = command.Argument,
                Position = command.Position,
                Target = command.Target,
                PrefabTypeId = EntityInfo.TypeIdOf(state.EntityManager, command.Prefab),
            };

            // A commanded unit goes as is; a selection only sends this player's own units.
            var isSingle = command.Unit != Entity.Null;
            foreach (var subject in CommandSubjects.Collect(command, listed, _selected))
            {
                if (rpc.Subjects.Length == CommandRpc.MaxSubjects)
                {
                    break;
                }

                if (isSingle || SystemAPI.GetComponent<Faction>(subject).Value == faction)
                {
                    rpc.Subjects.Add(subject);
                }
            }

            return rpc;
        }
    }
}
