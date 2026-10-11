using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Commands
{
    /// <summary>
    /// Turns received <see cref="CommandRpc"/>s into <see cref="PlayerCommand"/>s on the sender's player, in arrival
    /// order. A group command lists its units, keeping only those the player owns, in
    /// <see cref="PlayerCommandSubject"/>; observers' commands, and every command before
    /// <see cref="MatchState.Started"/>, are dropped.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct CommandReceiveSystem : ISystem
    {
        private EntityQuery _rpcs;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _rpcs = SystemAPI.QueryBuilder().WithAll<CommandRpc, ReceiveRpcCommandRequest>().Build();
            state.RequireForUpdate(_rpcs);
            state.RequireForUpdate<PrefabRegistry>();
            state.RequireForUpdate<MatchState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.GetSingleton<MatchState>().Started)
            {
                state.EntityManager.DestroyEntity(_rpcs);
                return;
            }

            var prefabs = SystemAPI.GetSingleton<PrefabRegistry>();
            foreach (var (rpc, request) in SystemAPI.Query<RefRO<CommandRpc>, RefRO<ReceiveRpcCommandRequest>>())
            {
                var connection = request.ValueRO.SourceConnection;
                if (SystemAPI.HasComponent<ConnectionPlayer>(connection))
                {
                    Apply(ref state, SystemAPI.GetComponent<ConnectionPlayer>(connection).Player, rpc.ValueRO, prefabs);
                }
            }

            state.EntityManager.DestroyEntity(_rpcs);
        }

        private void Apply(ref SystemState state, Entity player, in CommandRpc rpc, in PrefabRegistry prefabs)
        {
            var faction = SystemAPI.GetComponent<Player>(player).Faction;
            var command = new PlayerCommand
            {
                Type = (CommandType)rpc.Type,
                Queue = rpc.Queue,
                Argument = rpc.Argument,
                Position = rpc.Position,
                Target = rpc.Target,
                Prefab = prefabs.Find(rpc.PrefabTypeId),
            };

            if (rpc.Subjects.Length == 1)
            {
                var unit = rpc.Subjects[0];
                if (!IsOwned(ref state, unit, faction))
                {
                    return;
                }

                command.Unit = unit;
            }
            else
            {
                ListSubjects(ref state, player, rpc, faction, ref command);
            }

            SystemAPI.GetBuffer<PlayerCommand>(player).Add(command);
        }

        private void ListSubjects(ref SystemState state, Entity player, in CommandRpc rpc, byte faction,
            ref PlayerCommand command)
        {
            var listed = SystemAPI.GetBuffer<PlayerCommandSubject>(player);
            var start = listed.Length;
            foreach (var unit in rpc.Subjects)
            {
                if (IsOwned(ref state, unit, faction))
                {
                    listed.Add(new PlayerCommandSubject { Value = unit });
                }
            }

            command.SubjectStart = (ushort)start;
            command.SubjectCount = (ushort)(listed.Length - start);
        }

        private bool IsOwned(ref SystemState state, Entity entity, byte faction) =>
            SystemAPI.HasComponent<Faction>(entity) && SystemAPI.GetComponent<Faction>(entity).Value == faction;
    }
}
