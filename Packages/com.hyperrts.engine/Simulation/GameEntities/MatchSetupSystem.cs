using HyperRTS.Core;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
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

            if (setup.ClosedFactions != 0 && !state.WorldUnmanaged.IsClient())
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
                    setup.ClosedFactions |= Bit(faction);
                    SetTeam(ref relations, faction, 0);
                    entityManager.DestroyEntity(player);
                    continue;
                }

                var slot = setup.Slots[faction - 1];
                SetTeam(ref relations, faction, slot.Team);
                ApplySlot(entityManager, player, slot, tunings);
                ScaleStock(entityManager.GetBuffer<ResourceStock>(player), setup.StartingResourceScale);
            }

            entityManager.SetComponentData(match, relations);
            ApplyFog(entityManager, match, setup.Fog);
        }

        private static void ApplySlot(EntityManager entityManager, Entity player, in SlotSetup slot,
            NativeArray<AIDifficultyTuning> tunings)
        {
            var identity = entityManager.GetComponentData<Player>(player);
            identity.Name = slot.Name;
            identity.Color = slot.Color;
            entityManager.SetComponentData(player, identity);
            entityManager.AddComponentData(player, new PlayerSide { Value = slot.Side });

            if (slot.Control == PlayerControl.AI)
            {
                MakeAI(entityManager, player, TuningFor(tunings, slot.Difficulty));
                return;
            }

            entityManager.RemoveComponent<AIPlayer>(player);
            entityManager.RemoveComponent<IncomeMultiplier>(player);
            if (slot.Control == PlayerControl.LocalHuman)
            {
                entityManager.AddComponent<LocalPlayer>(player);
            }
            else
            {
                entityManager.RemoveComponent<LocalPlayer>(player);
            }
        }

        private static void MakeAI(EntityManager entityManager, Entity player, in AIDifficultyTuning tuning)
        {
            entityManager.RemoveComponent<LocalPlayer>(player);
            entityManager.AddComponentData(player, tuning.Tuning);
            if (!entityManager.HasBuffer<AIBuildStep>(player))
            {
                entityManager.AddBuffer<AIBuildStep>(player);
            }

            if (!AIDifficultyTuning.IsFairIncome(tuning.IncomeMultiplier))
            {
                entityManager.AddComponentData(player, new IncomeMultiplier { Value = tuning.IncomeMultiplier });
            }
            else
            {
                entityManager.RemoveComponent<IncomeMultiplier>(player);
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


        private static void SetTeam(ref FactionRelations relations, byte faction, byte team)
        {
            // Fog keeps one bit per team, so a team past the limit would silently never see anything.
            if (team >= FactionRelations.MaxTeams)
            {
                throw new System.ArgumentException(
                    $"Slot {faction} has team {team}; teams go up to {FactionRelations.MaxTeams - 1}.");
            }

            while (relations.Teams.Length <= faction)
            {
                relations.Teams.Add(0);
            }

            relations.Teams[faction] = team;
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
                if ((Bit(entityManager.GetComponentData<Faction>(entity).Value) & closed) == 0)
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

        private static uint Bit(byte faction) => faction < 32 ? 1u << faction : 0u;
    }
}
