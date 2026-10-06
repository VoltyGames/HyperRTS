using HyperRTS.Core;
using HyperRTS.Network.Players;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Commands
{
    /// <summary>
    /// Turns received <see cref="CommandRpc"/>s into <see cref="PlayerCommand"/>s on the sender's player, keeping only
    /// units it owns. A group command selects its units on the server, since command systems read selection; one
    /// group command per player per frame, so later ones wait a frame instead of sharing that selection.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct CommandReceiveSystem : ISystem
    {
        private EntityQuery _rpcs;
        private EntityQuery _prefabs;

        public void OnCreate(ref SystemState state)
        {
            _rpcs = SystemAPI.QueryBuilder().WithAll<CommandRpc, ReceiveRpcCommandRequest>().Build();
            _prefabs = SystemAPI.QueryBuilder().WithAll<EntityInfo, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build();
            state.RequireForUpdate(_rpcs);
        }

        public void OnUpdate(ref SystemState state)
        {
            var ghosts = new NativeHashMap<int, Entity>(256, Allocator.Temp);
            foreach (var (ghost, entity) in SystemAPI.Query<RefRO<GhostInstance>>().WithEntityAccess())
            {
                ghosts[ghost.ValueRO.ghostId] = entity;
            }

            var prefabs = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            foreach (var prefab in _prefabs.ToEntityArray(Allocator.Temp))
            {
                prefabs[state.EntityManager.GetComponentData<EntityInfo>(prefab).TypeId] = prefab;
            }

            var grouped = new NativeHashSet<Entity>(8, Allocator.Temp);
            foreach (var request in _rpcs.ToEntityArray(Allocator.Temp))
            {
                var rpc = state.EntityManager.GetComponentData<CommandRpc>(request);
                var connection = state.EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
                var player = PlayerOf(ref state, connection);
                var isGroup = rpc.Subjects.Length != 1;
                if (player != Entity.Null && isGroup && !grouped.Add(player))
                {
                    continue; // waits for next frame's selection
                }

                if (player != Entity.Null)
                {
                    Apply(ref state, player, rpc, ghosts, prefabs);
                }

                state.EntityManager.DestroyEntity(request);
            }
        }

        private void Apply(ref SystemState state, Entity player, in CommandRpc rpc, NativeHashMap<int, Entity> ghosts,
            NativeHashMap<int, Entity> prefabs)
        {
            var faction = SystemAPI.GetComponent<Player>(player).Faction;
            var command = new PlayerCommand
            {
                Type = (CommandType)rpc.Type,
                Queue = rpc.Queue,
                Argument = rpc.Argument,
                Position = rpc.Position,
                Target = ghosts.TryGetValue(rpc.Target, out var target) ? target : Entity.Null,
                Prefab = prefabs.TryGetValue(rpc.PrefabTypeId, out var prefab) ? prefab : Entity.Null,
            };

            var owned = new NativeHashSet<Entity>(rpc.Subjects.Length, Allocator.Temp);
            foreach (var id in rpc.Subjects)
            {
                if (ghosts.TryGetValue(id, out var unit) && IsOwned(ref state, unit, faction))
                {
                    owned.Add(unit);
                    command.Unit = unit;
                }
            }

            if (rpc.Subjects.Length != 1)
            {
                command.Unit = Entity.Null;
                Select(ref state, faction, owned);
            }
            else if (command.Unit == Entity.Null)
            {
                return;
            }

            SystemAPI.GetBuffer<PlayerCommand>(player).Add(command);
        }

        private void Select(ref SystemState state, byte faction, NativeHashSet<Entity> units)
        {
            foreach (var (owner, selected, entity) in SystemAPI.Query<RefRO<Faction>, EnabledRefRW<Selected>>()
                         .WithPresent<Selected>().WithEntityAccess())
            {
                if (owner.ValueRO.Value == faction)
                {
                    selected.ValueRW = units.Contains(entity);
                }
            }
        }

        private bool IsOwned(ref SystemState state, Entity entity, byte faction) =>
            SystemAPI.HasComponent<Faction>(entity) && SystemAPI.GetComponent<Faction>(entity).Value == faction;

        private Entity PlayerOf(ref SystemState state, Entity connection)
        {
            if (!SystemAPI.HasComponent<NetworkId>(connection))
            {
                return Entity.Null;
            }

            var networkId = SystemAPI.GetComponent<NetworkId>(connection).Value;
            foreach (var (link, entity) in SystemAPI.Query<RefRO<PlayerConnection>>().WithEntityAccess())
            {
                if (link.ValueRO.NetworkId == networkId)
                {
                    return entity;
                }
            }

            return Entity.Null;
        }
    }
}
