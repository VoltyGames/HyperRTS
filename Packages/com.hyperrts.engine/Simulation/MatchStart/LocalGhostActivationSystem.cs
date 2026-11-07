using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.MatchStart
{
    /// <summary>
    /// Enables prespawned ghosts in single player, where netcode's prespawn systems never run, so they play as plain entities.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct LocalGhostActivationSystem : ISystem
    {
        private EntityQuery _waiting;

        public void OnCreate(ref SystemState state)
        {
            _waiting = SystemAPI.QueryBuilder().WithAll<PreSpawnedGhostIndex, Disabled>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build();
            state.RequireForUpdate(_waiting);

            // Exists in every world so others can order after it; Netcode handles the client and server worlds.
            state.Enabled = !state.WorldUnmanaged.IsServer() && !state.WorldUnmanaged.IsClient();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            foreach (var ghost in _waiting.ToEntityArray(Allocator.Temp))
            {
                entityManager.SetEnabled(ghost, true);
            }
        }
    }
}
