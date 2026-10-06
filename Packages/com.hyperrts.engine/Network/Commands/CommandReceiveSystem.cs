using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Commands
{
    /// <summary>
    /// Turns received <see cref="CommandRpc"/>s into <see cref="PlayerCommand"/>s on the sender's player, in arrival
    /// order. A group command lists its units, keeping only those the player owns, in
    /// <see cref="PlayerCommandSubject"/>; observers' commands are dropped.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct CommandReceiveSystem : ISystem
    {
        private EntityQuery _rpcs;
        private EntityQuery _prefabs;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _rpcs = SystemAPI.QueryBuilder().WithAll<CommandRpc, ReceiveRpcCommandRequest>().Build();
            _prefabs = SystemAPI.QueryBuilder().WithAll<EntityInfo, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build();
            state.RequireForUpdate(_rpcs);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var prefabs = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            PrefabLookup.ByTypeId(_prefabs, prefabs);

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

        private void Apply(ref SystemState state, Entity player, in CommandRpc rpc, NativeHashMap<int, Entity> prefabs)
        {
            var faction = SystemAPI.GetComponent<Player>(player).Faction;
            var command = new PlayerCommand
            {
                Type = (CommandType)rpc.Type,
                Queue = rpc.Queue,
                Argument = rpc.Argument,
                Position = rpc.Position,
                Target = rpc.Target,
                Prefab = prefabs.TryGetValue(rpc.PrefabTypeId, out var prefab) ? prefab : Entity.Null,
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

            SystemAPI.GetBuffer<PlayerCommand>(player).Add(command);
        }

        private bool IsOwned(ref SystemState state, Entity entity, byte faction) =>
            SystemAPI.HasComponent<Faction>(entity) && SystemAPI.GetComponent<Faction>(entity).Value == faction;
    }
}
