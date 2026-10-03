using HyperRTS.Core;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Publishes <see cref="LocalFogView"/> after each fog restamp so every consumer applies one rule.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(FogOfWarSystem))]
    public partial struct LocalFogViewSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.CreateSingleton<LocalFogView>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var view = new LocalFogView { Viewer = Faction.Neutral };
            var enabled = SystemAPI.TryGetSingleton(out MapSettings map) && map.FogOfWar
                && SystemAPI.TryGetSingleton(out view.Fog) && view.Fog.IsCreated
                && SystemAPI.TryGetSingleton(out view.Relations);

            foreach (var player in SystemAPI.Query<RefRO<Player>>().WithAll<LocalPlayer>())
            {
                view.Viewer = player.ValueRO.Faction;
            }

            view.Active = enabled && view.Relations.TeamOf(view.Viewer) != 0;
            SystemAPI.SetSingleton(view);
        }
    }
}
