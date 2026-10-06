using HyperRTS.Core;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Players
{
    /// <summary>
    /// Turns each baked player into its own ghost prefab, the same way on server and client, and the server spawns
    /// one ghost per slot a frame later (Netcode finishes preparing new prefabs on its next update).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct PlayerGhostSystem : ISystem
    {
        private EntityQuery _baked;
        private EntityQuery _prefabs;
        private bool _spawnPending;

        public void OnCreate(ref SystemState state)
        {
            _baked = SystemAPI.QueryBuilder().WithAll<Player>().WithNone<GhostInstance>().Build();
            _prefabs = SystemAPI.QueryBuilder().WithAll<Player, Prefab>().WithOptions(EntityQueryOptions.IncludePrefab)
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_spawnPending)
            {
                _spawnPending = false;
                SpawnPlayers(ref state);
            }

            if (_baked.IsEmpty)
            {
                return;
            }

            var entityManager = state.EntityManager;
            foreach (var player in _baked.ToEntityArray(Allocator.Temp))
            {
                entityManager.RemoveComponent<LocalPlayer>(player);
                var faction = entityManager.GetComponentData<Player>(player).Faction;
                GhostPrefabCreation.ConvertToGhostPrefab(entityManager, player, new GhostPrefabCreation.Config
                {
                    Name = $"HyperRTS Player {faction}",
                    Importance = 100,
                    SupportedGhostModes = GhostModeMask.Interpolated,
                    DefaultGhostMode = GhostMode.Interpolated,
                    OptimizationMode = GhostOptimizationMode.Dynamic,
                });
            }

            _spawnPending = state.WorldUnmanaged.IsServer();
        }

        private void SpawnPlayers(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            foreach (var prefab in _prefabs.ToEntityArray(Allocator.Temp))
            {
                var player = entityManager.Instantiate(prefab);
                entityManager.AddComponent<PlayerConnection>(player);
            }
        }
    }
}
