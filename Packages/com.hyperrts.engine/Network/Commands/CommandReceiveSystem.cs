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
        private EntityQuery _players;

        public void OnCreate(ref SystemState state)
        {
            _rpcs = SystemAPI.QueryBuilder().WithAll<CommandRpc, ReceiveRpcCommandRequest>().Build();
            _prefabs = SystemAPI.QueryBuilder().WithAll<EntityInfo, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build();
            _players = SystemAPI.QueryBuilder().WithAll<PlayerConnection>().Build();
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
            PrefabLookup.ByTypeId(_prefabs, prefabs);
            var players = PlayerConnections.ByNetworkId(_players);

            var grouped = new NativeHashSet<Entity>(8, Allocator.Temp);
            var handled = new NativeList<Entity>(Allocator.Temp);
            foreach (var request in _rpcs.ToEntityArray(Allocator.Temp))
            {
                var rpc = state.EntityManager.GetComponentData<CommandRpc>(request);
                var connection = state.EntityManager.GetComponentData<ReceiveRpcCommandRequest>(request).SourceConnection;
                if (!TryGetPlayer(ref state, players, connection, out var player))
                {
                    handled.Add(request);
                    continue;
                }

                var isGroup = rpc.Subjects.Length != 1;
                if (isGroup && !grouped.Add(player))
                {
                    continue; // waits for next frame's selection
                }

                Apply(ref state, player, rpc, isGroup, ghosts, prefabs);
                handled.Add(request);
            }

            state.EntityManager.DestroyEntity(handled.AsArray());
        }

        private void Apply(ref SystemState state, Entity player, in CommandRpc rpc, bool isGroup,
            NativeHashMap<int, Entity> ghosts, NativeHashMap<int, Entity> prefabs)
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

            if (isGroup)
            {
                var owned = new NativeHashSet<Entity>(rpc.Subjects.Length, Allocator.Temp);
                foreach (var id in rpc.Subjects)
                {
                    if (ghosts.TryGetValue(id, out var unit) && IsOwned(ref state, unit, faction))
                    {
                        owned.Add(unit);
                    }
                }

                Select(ref state, faction, owned);
            }
            else
            {
                if (!ghosts.TryGetValue(rpc.Subjects[0], out var unit) || !IsOwned(ref state, unit, faction))
                {
                    return;
                }

                command.Unit = unit;
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

        private bool TryGetPlayer(ref SystemState state, NativeHashMap<int, Entity> players, Entity connection,
            out Entity player)
        {
            player = Entity.Null;
            if (!SystemAPI.HasComponent<NetworkId>(connection))
            {
                return false;
            }

            return players.TryGetValue(SystemAPI.GetComponent<NetworkId>(connection).Value, out player);
        }

        private bool IsOwned(ref SystemState state, Entity entity, byte faction) =>
            SystemAPI.HasComponent<Faction>(entity) && SystemAPI.GetComponent<Faction>(entity).Value == faction;
    }
}
