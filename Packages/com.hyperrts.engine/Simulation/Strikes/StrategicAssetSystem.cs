using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Strikes
{
    /// <summary>
    /// Tracks each player's <see cref="StrategicCharge"/> from their strategic assets, and holds every strike ability
    /// on cooldown and every strategic weapon empty while the match switches strategic assets off.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AbilitySystem))]
    public partial struct StrategicAssetSystem : ISystem
    {
        private EntityQuery _mounts;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _mounts = new EntityQueryBuilder(Allocator.Temp).WithAllRW<WeaponMount>().Build(ref state);
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

            if (!enabled)
            {
                HoldStrikes(ref state);
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

        private void HoldStrikes(ref SystemState state)
        {
            foreach (var (abilities, states, entity) in SystemAPI
                         .Query<DynamicBuffer<Ability>, DynamicBuffer<AbilityCooldown>>()
                         .WithAny<BomberStrike, MissileStrike>().WithEntityAccess())
            {
                var id = SystemAPI.HasComponent<BomberStrike>(entity)
                    ? SystemAPI.GetComponent<BomberStrike>(entity).AbilityId
                    : SystemAPI.GetComponent<MissileStrike>(entity).AbilityId;
                states.ElementAt(AbilityCooldown.Find(states, id)).Remaining = abilities[Ability.Find(abilities, id)].Cooldown;
            }

            state.Dependency = new EmptyStrategicWeaponsJob
            {
                Mounts = SystemAPI.GetBufferTypeHandle<WeaponMount>(),
            }.ScheduleParallel(_mounts, state.Dependency);
        }

        [BurstCompile]
        private struct EmptyStrategicWeaponsJob : IJobChunk
        {
            public BufferTypeHandle<WeaponMount> Mounts;

            // The writable accessor is taken only for a unit with a strategic round to clear, so other chunks keep their version.
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var read = chunk.GetBufferAccessorRO(ref Mounts);
                var entities = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entities.NextEntityIndex(out var i))
                {
                    if (!HasLoadedStrategic(read[i]))
                    {
                        continue;
                    }

                    var mounts = chunk.GetBufferAccessorRW(ref Mounts)[i];
                    for (var m = 0; m < mounts.Length; m++)
                    {
                        ref var mount = ref mounts.ElementAt(m);
                        if (mount.Weapon.Strategic)
                        {
                            mount.Ammo.Current = 0;
                            mount.Ammo.ReloadElapsed = 0f;
                        }
                    }
                }
            }

            private static bool HasLoadedStrategic(in DynamicBuffer<WeaponMount> mounts)
            {
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    if (mount.Weapon.Strategic && (mount.Ammo.Current != 0 || mount.Ammo.ReloadElapsed != 0f))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
