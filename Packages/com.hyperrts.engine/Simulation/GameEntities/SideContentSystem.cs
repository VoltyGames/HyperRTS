using HyperRTS.Core;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.GameEntities
{
    /// <summary>
    /// Once the <see cref="MatchSetup"/> is applied (or at once when the scene plays as baked), gives every player its
    /// side's AI build order and spawns its side's start kit at its <see cref="PlayerSpawn"/>, on the ground. Closed
    /// slots have no player by then, so they get nothing. Not Burst: it runs once and a missing side throws.
    /// </summary>
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(MatchSetupSystem))]
    public partial struct SideContentSystem : ISystem
    {
        private EntityQuery _match;
        private EntityQuery _players;

        public void OnCreate(ref SystemState state)
        {
            // Disabled included: in network worlds the Match is a prespawned ghost Netcode keeps disabled at first.
            _match = SystemAPI.QueryBuilder().WithAll<MatchSide, MapSettings>().WithNone<SidesApplied>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build();
            // Prefabs included: on the server the baked players may already be ghost prefabs.
            _players = SystemAPI.QueryBuilder().WithAll<Player, PlayerSide>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build();
            state.RequireForUpdate(_match);
        }

        public void OnUpdate(ref SystemState state)
        {
            var waitingForSetup = SystemAPI.TryGetSingleton<MatchSetup>(out var setup) && !setup.Applied;
            if (waitingForSetup || _players.IsEmpty)
            {
                return;
            }

            var entityManager = state.EntityManager;
            var match = _match.GetSingletonEntity();
            if (!entityManager.GetBuffer<MatchSide>(match).IsEmpty)
            {
                Apply(ref state, match);
            }

            entityManager.AddComponent<SidesApplied>(match);
        }

        private void Apply(ref SystemState state, Entity match)
        {
            var entityManager = state.EntityManager;
            if (!SystemAPI.TryGetSingleton<TerrainHeight>(out var terrain))
            {
                throw new System.InvalidOperationException("The Match has sides but no Terrain Height to stand kits on.");
            }

            // Copied out: spawning kits would invalidate the buffers.
            var sides = entityManager.GetBuffer<MatchSide>(match).ToNativeArray(Allocator.Temp);
            var kit = entityManager.GetBuffer<SideStartItem>(match).ToNativeArray(Allocator.Temp);
            var steps = entityManager.GetBuffer<SideBuildStep>(match).ToNativeArray(Allocator.Temp);
            var players = _players.ToEntityArray(Allocator.Temp);
            // Checked first, so a bad side spawns nothing rather than half the kits.
            foreach (var player in players)
            {
                RequireSide(sides, entityManager.GetComponentData<PlayerSide>(player).Value,
                    entityManager.GetComponentData<Player>(player).Faction);
            }

            var spawned = 0u;
            foreach (var player in players)
            {
                var faction = entityManager.GetComponentData<Player>(player).Faction;
                var side = entityManager.GetComponentData<PlayerSide>(player).Value;
                if (entityManager.HasBuffer<AIBuildStep>(player))
                {
                    SetBuildOrder(entityManager.GetBuffer<AIBuildStep>(player), steps, side);
                }

                // A ghost prefab and its instance are one player: one kit.
                if ((spawned & Faction.Bit(faction)) != 0)
                {
                    continue;
                }

                spawned |= Faction.Bit(faction);
                var spawn = entityManager.GetComponentData<PlayerSpawn>(player);
                SpawnKit(entityManager, kit, side, faction, spawn, terrain);
            }
        }

        private static void RequireSide(NativeArray<MatchSide> sides, byte side, byte faction)
        {
            foreach (var entry in sides)
            {
                if (entry.Value == side)
                {
                    return;
                }
            }

            throw new System.InvalidOperationException(
                $"Player {faction} plays side {side}, which the Match's sides don't list.");
        }

        private static void SetBuildOrder(DynamicBuffer<AIBuildStep> buildOrder, NativeArray<SideBuildStep> steps,
            byte side)
        {
            buildOrder.Clear();
            foreach (var step in steps)
            {
                if (step.Side == side)
                {
                    buildOrder.Add(new AIBuildStep { Prefab = step.Prefab, Count = step.Count });
                }
            }
        }

        private static void SpawnKit(EntityManager entityManager, NativeArray<SideStartItem> kit, byte side,
            byte faction, in PlayerSpawn spawn, in TerrainHeight terrain)
        {
            math.sincos(spawn.Yaw, out var sin, out var cos);
            var forward = new float2(sin, cos);
            var right = new float2(cos, -sin);
            foreach (var item in kit)
            {
                if (item.Side != side)
                {
                    continue;
                }

                var ground = spawn.Position.xz + right * item.Offset.x + forward * item.Offset.y;
                var transform = entityManager.GetComponentData<LocalTransform>(item.Prefab);
                transform.Position = new float3(ground.x, terrain.Height(ground), ground.y);
                transform.Rotation = quaternion.RotateY(spawn.Yaw + item.Yaw);
                var entity = entityManager.Instantiate(item.Prefab);
                entityManager.SetComponentData(entity, transform);
                entityManager.SetComponentData(entity, new Faction { Value = faction });
            }
        }
    }
}
