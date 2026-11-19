using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Strikes
{
    /// <summary>
    /// Tracks each player's <see cref="StrategicCharge"/> from their strategic assets; the charge shows nothing while
    /// the match switches strategic assets off.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AbilitySystem))]
    public partial struct StrategicAssetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatchRules>();
            state.RequireForUpdate<PlayerRegistry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var enabled = SystemAPI.GetSingleton<MatchRules>().StrategicAssets;
            var players = SystemAPI.GetSingleton<PlayerRegistry>();
            foreach (var charge in SystemAPI.Query<RefRW<StrategicCharge>>())
            {
                charge.ValueRW = default;
            }

            foreach (var (asset, abilities, states, owner) in SystemAPI
                         .Query<RefRW<StrategicAsset>, DynamicBuffer<Ability>, DynamicBuffer<AbilityCooldown>, RefRO<Owner>>())
            {
                var id = asset.ValueRO.AbilityId;
                var ability = abilities[Ability.Find(abilities, id)];
                ref var cooldown = ref states.ElementAt(AbilityCooldown.Find(states, id));
                var holder = owner.ValueRO.Value;
                if (holder != asset.ValueRO.Owner)
                {
                    cooldown.Remaining = ability.Cooldown;
                    asset.ValueRW.Owner = holder;
                }

                if (enabled && holder != Owner.Neutral)
                {
                    Publish(ref state, players[holder], ability, cooldown);
                }
            }
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
    }
}
