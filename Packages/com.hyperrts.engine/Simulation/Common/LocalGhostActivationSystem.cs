using HyperRTS.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Netcode keeps prespawned ghosts (scene objects with a GhostAuthoringComponent, like the Match) disabled until
    /// its prespawn systems register them, which only run in client and server worlds. Single player has neither, so
    /// this enables them there and they play as plain entities.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct LocalGhostActivationSystem : ISystem
    {
        private EntityQuery _waiting;

        public void OnCreate(ref SystemState state)
        {
            _waiting = SystemAPI.QueryBuilder().WithAll<PreSpawnedGhostIndex, Disabled>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build();
            state.RequireForUpdate(_waiting);
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
