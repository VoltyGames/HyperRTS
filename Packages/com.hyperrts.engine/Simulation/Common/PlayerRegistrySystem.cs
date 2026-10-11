using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Rebuilds the <see cref="PlayerRegistry"/> from the <see cref="Player"/> entities at the start of every frame.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct PlayerRegistrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var registry = state.EntityManager.CreateSingleton<PlayerRegistry>();
            state.EntityManager.SetName(registry, "PlayerRegistry");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var registry = new PlayerRegistry();
            foreach (var (player, entity) in SystemAPI.Query<RefRO<Player>>().WithEntityAccess())
            {
                registry.Set(player.ValueRO.Faction, entity);
            }

            SystemAPI.SetSingleton(registry);
        }
    }
}
