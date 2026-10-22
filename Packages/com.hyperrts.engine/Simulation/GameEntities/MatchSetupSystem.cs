using HyperRTS.Core;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.Transforms;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>
    /// Applies the <see cref="MatchSetup"/> (from <see cref="MatchSetupRequest"/>) to the baked players once, the same
    /// way in every world, so server and clients build identical player ghosts. Authoritative worlds also remove
    /// everything closed slots own. Not Burst: it runs once and reads the managed request.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(LocalGhostActivationSystem))]
    public partial struct MatchSetupSystem : ISystem
    {
        private EntityQuery _match;
        private EntityQuery _players;
        private EntityQuery _owned;
        private bool _retryOwned;

        public void OnCreate(ref SystemState state)
        {
            _players = SystemAPI.QueryBuilder().WithAll<Player>().WithNone<GhostInstance>().Build();
            _owned = SystemAPI.QueryBuilder().WithAll<Faction>().WithNone<Parent>().Build();
            _owned.SetChangedVersionFilter(ComponentType.ReadOnly<Faction>());
            // Disabled included: in network worlds the Match is a prespawned ghost Netcode keeps disabled at first,
            // and the setup must land before the players become ghosts.
            _match = SystemAPI.QueryBuilder().WithAll<FactionRelations, MapSettings>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build();
            state.RequireForUpdate(_match);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!TryGetSetup(ref state, out var setupEntity))
            {
                return;
            }

            var setup = state.EntityManager.GetComponentData<MatchSetup>(setupEntity);
            if (!setup.Applied)
            {
                if (_players.IsEmpty)
                {
                    return;
                }

                Apply(ref state, ref setup);
                setup.Applied = true;
                state.EntityManager.SetComponentData(setupEntity, setup);
            }

            if (setup.ClosedFactions != 0 && SimulationWorlds.IsAuthoritative(state.WorldUnmanaged))
            {
                RemoveClosed(ref state, setup.ClosedFactions);
            }
        }

        private bool TryGetSetup(ref SystemState state, out Entity setupEntity)
        {
            if (SystemAPI.TryGetSingletonEntity<MatchSetup>(out setupEntity))
            {
                return true;
            }

            if (!MatchSetupRequest.TryGet(out var requested))
            {
                return false;
            }

            setupEntity = state.EntityManager.CreateSingleton(requested, "MatchSetup");
            return true;
        }

        private void Apply(ref SystemState state, ref MatchSetup setup)
        {
            var entityManager = state.EntityManager;
            var match = _match.GetSingletonEntity();
            var relations = entityManager.GetComponentData<FactionRelations>(match);
            // Copied out: the structural changes that follow would invalidate the buffer.
            var tunings = entityManager.GetBuffer<AIDifficultyTuning>(match, true).ToNativeArray(Allocator.Temp);
            var players = _players.ToEntityArray(Allocator.Temp);
            foreach (var player in players)
            {
                var faction = entityManager.GetComponentData<Player>(player).Faction;
                if (!setup.IsOpen(faction))
                {
                    setup.ClosedFactions |= Faction.Bit(faction);
                    relations.SetTeam(faction, 0);
                    entityManager.DestroyEntity(player);
                    continue;
                }

                var slot = setup.Slots[faction - 1];
                relations.SetTeam(faction, slot.Team);
                ApplySlot(entityManager, player, slot, tunings, setup.Seed);
                ScaleStock(entityManager.GetBuffer<ResourceStock>(player), setup.StartingResourceScale);
            }

            entityManager.SetComponentData(match, relations);
            ApplyFog(entityManager, match, setup.Fog);
        }

        private static void ApplySlot(EntityManager entityManager, Entity player, in SlotSetup slot,
            NativeArray<AIDifficultyTuning> tunings, uint seed)
        {
            var identity = entityManager.GetComponentData<Player>(player);
            identity.Name = slot.Name;
            identity.Color = slot.Color;
            entityManager.SetComponentData(player, identity);
            entityManager.AddComponentData(player, new PlayerSide { Value = slot.Side });

            if (slot.Control == PlayerControl.AI)
            {
                MakeAI(entityManager, player, TuningFor(tunings, slot.Difficulty), AIRoutes.SeedFor(seed, identity.Faction));
                return;
            }

            entityManager.RemoveComponent<AIPlayer>(player);
            SetIncomeBonus(entityManager, player, 1f);
            if (slot.Control == PlayerControl.LocalHuman)
            {
                entityManager.AddComponent<LocalPlayer>(player);
            }
            else
            {
                entityManager.RemoveComponent<LocalPlayer>(player);
            }
        }

        private static void MakeAI(EntityManager entityManager, Entity player, in AIDifficultyTuning tuning,
            uint routeSeed)
        {
            entityManager.RemoveComponent<LocalPlayer>(player);
            var ai = tuning.Tuning;
            ai.RouteSeed = routeSeed;
            entityManager.AddComponentData(player, ai);
            if (!entityManager.HasBuffer<AIBuildStep>(player))
            {
                entityManager.AddBuffer<AIBuildStep>(player);
            }

            SetIncomeBonus(entityManager, player, tuning.IncomeMultiplier);
        }

        /// <summary>Replaces the player's difficulty income modifier; a fair multiplier leaves none.</summary>
        private static void SetIncomeBonus(EntityManager entityManager, Entity player, float multiplier)
        {
            var modifiers = entityManager.GetBuffer<StatModifier>(player);
            StatMath.RemoveKind(modifiers, StatSourceKind.Difficulty);
            if (AIDifficultyTuning.TryGetIncomeBonus(multiplier, out var bonus))
            {
                modifiers.Add(bonus);
            }
        }

        private static AIDifficultyTuning TuningFor(NativeArray<AIDifficultyTuning> tunings, AIDifficulty difficulty)
        {
            foreach (var tuning in tunings)
            {
                if (tuning.Difficulty == difficulty)
                {
                    return tuning;
                }
            }

            throw new System.InvalidOperationException($"The Match baked no tuning for {difficulty}.");
        }

        private static void ScaleStock(DynamicBuffer<ResourceStock> stock, float scale)
        {
            for (var i = 0; i < stock.Length; i++)
            {
                var element = stock[i];
                element.Amount = (int)math.round(element.Amount * scale);
                stock[i] = element;
            }
        }

        private static void ApplyFog(EntityManager entityManager, Entity match, FogOverride fog)
        {
            if (fog == FogOverride.Map)
            {
                return;
            }

            var map = entityManager.GetComponentData<MapSettings>(match);
            map.FogOfWar = fog == FogOverride.On;
            entityManager.SetComponentData(match, map);
        }

        /// <summary>
        /// Destroys what closed slots own as it streams in. A prespawned ghost waits for its ghost id on the server,
        /// so clients get a despawn for it instead of a missing prespawn.
        /// </summary>
        private void RemoveClosed(ref SystemState state, uint closed)
        {
            if (_retryOwned)
            {
                _owned.ResetFilter();
            }

            var entityManager = state.EntityManager;
            var isServer = state.WorldUnmanaged.IsServer();
            var doomed = new NativeList<Entity>(Allocator.Temp);
            _retryOwned = false;
            foreach (var entity in _owned.ToEntityArray(Allocator.Temp))
            {
                if ((Faction.Bit(entityManager.GetComponentData<Faction>(entity).Value) & closed) == 0)
                {
                    continue;
                }

                if (isServer && !HasGhostId(entityManager, entity))
                {
                    _retryOwned = true;
                    continue;
                }

                doomed.Add(entity);
            }

            _owned.SetChangedVersionFilter(ComponentType.ReadOnly<Faction>());
            entityManager.DestroyEntity(doomed.AsArray());
        }

        private static bool HasGhostId(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<PreSpawnedGhostIndex>(entity))
            {
                return true;
            }

            return entityManager.HasComponent<GhostInstance>(entity) &&
                   entityManager.GetComponentData<GhostInstance>(entity).ghostId != 0;
        }

    }
}
