using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Strikes
{
    /// <summary>
    /// Runs strategic assets on their abilities: tracks each player's <see cref="StrategicCharge"/> and launches a <see cref="BombingFlight"/> for each firing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AbilitySystem))]
    public partial struct StrategicAssetSystem : ISystem
    {
        private PrefabSpawner _spawner;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchRules>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<AbilityEvents>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            _spawner = new PrefabSpawner(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawner.Update(ref state);
            var enabled = SystemAPI.GetSingleton<MatchRules>().StrategicAssets;
            var players = SystemAPI.GetSingleton<PlayerRegistry>();
            foreach (var charge in SystemAPI.Query<RefRW<StrategicCharge>>())
            {
                charge.ValueRW = default;
            }

            foreach (var (asset, abilities, states, owner) in SystemAPI
                         .Query<RefRW<StrategicAsset>, DynamicBuffer<Ability>, DynamicBuffer<AbilityCooldown>, RefRO<Owner>>()
                         .WithNone<Dead>())
            {
                var id = asset.ValueRO.AbilityId;
                var ability = abilities[Ability.Find(abilities, id)];
                ref var cooldown = ref states.ElementAt(AbilityCooldown.Find(states, id));
                var holder = owner.ValueRO.Value;
                if (!enabled || holder != asset.ValueRO.Owner)
                {
                    cooldown.Remaining = ability.Cooldown;
                    asset.ValueRW.Owner = holder;
                }

                if (enabled && holder != Owner.Neutral)
                {
                    Publish(ref state, players[holder], ability, cooldown);
                }
            }

            Launch(ref state, players);
        }

        /// <summary>A player with several assets shows the one nearest its charge.</summary>
        private void Publish(ref SystemState state, Entity player, in Ability ability, in AbilityCooldown cooldown)
        {
            var charge = SystemAPI.GetComponentRW<StrategicCharge>(player);
            var remaining = math.max(0f, cooldown.Remaining);
            if (charge.ValueRO.HasAsset && charge.ValueRO.Remaining <= remaining)
            {
                return;
            }

            charge.ValueRW = new StrategicCharge { HasAsset = true, Remaining = remaining, Duration = ability.Cooldown };
        }

        private void Launch(ref SystemState state, in PlayerRegistry players)
        {
            var map = SystemAPI.GetSingleton<MapSettings>();
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            foreach (var used in SystemAPI.GetSingletonBuffer<AbilityActivation>(true))
            {
                if (!SystemAPI.HasComponent<StrategicAsset>(used.Caster))
                {
                    continue;
                }

                var asset = SystemAPI.GetComponent<StrategicAsset>(used.Caster);
                if (asset.AbilityId != used.AbilityId)
                {
                    continue;
                }

                var start = SystemAPI.GetComponent<PlayerSpawn>(players[used.Owner]).Position;
                var route = DropRoute.Plan(map, start, used.Position, asset.Altitude);
                var flight = SystemAPI.GetComponent<Flight>(asset.Aircraft);
                var aircraft = route.Launch(ecb, _spawner, asset.Aircraft, flight, asset.Altitude, used.Owner);
                ecb.AddComponent(aircraft,
                    new BombingFlight { RevealSeconds = asset.RevealSeconds, Bombs = asset.Bombs });
            }
        }
    }
}
