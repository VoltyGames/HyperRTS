using HyperRTS.Core;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Airdrops
{
    /// <summary>
    /// Runs strategic assets on their abilities: holds the charge while <see cref="MatchRules.StrategicAssets"/> is
    /// off, starts it over for a new owner, publishes each player's <see cref="StrategicCharge"/> and launches a
    /// <see cref="BombingFlight"/> for each firing. <c>AbilitySystem</c> already pauses the charge while offline.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AbilitySystem))]
    public partial struct StrategicAssetSystem : ISystem
    {
        private ComponentLookup<PostTransformMatrix> _stretchLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchRules>();
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<AbilityEvents>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            _stretchLookup = state.GetComponentLookup<PostTransformMatrix>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _stretchLookup.Update(ref state);
            var enabled = SystemAPI.GetSingleton<MatchRules>().StrategicAssets;
            var players = SystemAPI.GetSingleton<PlayerRegistry>();
            foreach (var charge in SystemAPI.Query<RefRW<StrategicCharge>>())
            {
                charge.ValueRW = default;
            }

            foreach (var (asset, abilities, faction) in SystemAPI
                         .Query<RefRW<StrategicAsset>, DynamicBuffer<Ability>, RefRO<Faction>>().WithNone<Dead>())
            {
                ref var ability = ref abilities.ElementAt(AbilityRules.FindAbility(abilities, asset.ValueRO.AbilityId));
                var owner = faction.ValueRO.Value;
                if (!enabled || owner != asset.ValueRO.Owner)
                {
                    ability.CooldownRemaining = ability.Cooldown;
                    asset.ValueRW.Owner = owner;
                }

                if (enabled && owner != Faction.Neutral)
                {
                    Publish(ref state, players[owner], ability);
                }
            }

            Launch(ref state, players);
        }

        /// <summary>A player with several assets shows the one nearest its charge.</summary>
        private void Publish(ref SystemState state, Entity player, in Ability ability)
        {
            var charge = SystemAPI.GetComponentRW<StrategicCharge>(player);
            var remaining = math.max(0f, ability.CooldownRemaining);
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

                var start = SystemAPI.GetComponent<PlayerSpawn>(players[used.Faction]).Position;
                var route = DropRoute.Plan(map, start, used.Position, asset.Altitude);
                var aircraft = ecb.Spawn(asset.Aircraft, SystemAPI.GetComponent<LocalTransform>(asset.Aircraft),
                    _stretchLookup, route.Entry, route.Facing, used.Faction);
                var flight = SystemAPI.GetComponent<Flight>(asset.Aircraft);
                flight.Altitude = asset.Altitude;
                ecb.SetComponent(aircraft, flight);
                ecb.AddComponent(aircraft, new BombingFlight
                {
                    Route = route, RevealSeconds = asset.RevealSeconds, Bombs = asset.Bombs,
                });
                ecb.SetComponent(aircraft, new MoveDestination { Value = route.Target });
                ecb.SetComponentEnabled<MoveDestination>(aircraft, true);
            }
        }
    }
}
