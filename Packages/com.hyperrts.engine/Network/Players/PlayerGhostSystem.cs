using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Network.Players
{
    /// <summary>
    /// Turns each baked player into its own ghost prefab, the same way on server and client, and the server spawns
    /// one ghost per new slot a frame later (Netcode finishes preparing new prefabs on its next update).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(MatchSetupSystem))]
    public partial struct PlayerGhostSystem : ISystem
    {
        private EntityQuery _baked;

        // Players can stream in over several frames, so only the prefabs converted last frame are spawned.
        private NativeList<Entity> _pending;

        public void OnCreate(ref SystemState state)
        {
            _baked = SystemAPI.QueryBuilder().WithAll<Player>().WithNone<GhostInstance>().Build();
            _pending = new NativeList<Entity>(8, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state) => _pending.Dispose();

        public void OnUpdate(ref SystemState state)
        {
            SpawnPending(ref state);
            if (_baked.IsEmpty)
            {
                return;
            }

            var isServer = state.WorldUnmanaged.IsServer();
            foreach (var player in _baked.ToEntityArray(Allocator.Temp))
            {
                Convert(state.EntityManager, player);
                if (isServer)
                {
                    _pending.Add(player);
                }
            }
        }

        private static void Convert(EntityManager entityManager, Entity player)
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

        private void SpawnPending(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            foreach (var prefab in _pending)
            {
                if (!entityManager.Exists(prefab))
                {
                    continue;
                }

                var player = entityManager.Instantiate(prefab);
                entityManager.AddComponent<PlayerConnection>(player);
            }

            _pending.Clear();
        }
    }
}
