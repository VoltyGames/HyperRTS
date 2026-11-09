using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>A load test: two armies spawned in front of opposite bases attack-move at each other.</summary>
    internal static class BattleCheat
    {
        private const float Spacing = 3.5f;
        private const float FrontDistance = 25f;

        /// <summary>
        /// Spawns <paramref name="perSide"/> of <paramref name="prefab"/> in front of the first player's base and of
        /// the farthest hostile base, each army attack-moving to the other base.
        /// </summary>
        public static void Start(EntityManager entityManager, Entity prefab, int perSide)
        {
            if (!TryFindOpposingBases(entityManager, out var first, out var second))
            {
                Debug.LogWarning("[HyperRTS] A battle needs two hostile owners with buildings.");
                return;
            }

            PlayWorld.TryGetSingleton(entityManager, out TerrainHeight terrain);
            Army(entityManager, prefab, perSide, first, second, terrain);
            Army(entityManager, prefab, perSide, second, first, terrain);
        }

        private static void Army(EntityManager entityManager, Entity prefab, int count, Base home, Base enemy,
            in TerrainHeight terrain)
        {
            var forward = math.normalizesafe(new float3(enemy.Position.x - home.Position.x, 0f,
                enemy.Position.z - home.Position.z));
            var right = new float3(forward.z, 0f, -forward.x);
            var columns = (int)math.ceil(math.sqrt(count));
            var front = home.Position + forward * FrontDistance;

            var rotation = entityManager.GetComponentData<LocalTransform>(prefab).Rotation;
            var spawned = new NativeArray<Entity>(count, Allocator.Temp);
            for (var i = 0; i < count; i++)
            {
                var column = i % columns - (columns - 1) * 0.5f;
                var row = i / columns;
                var position = front + right * (column * Spacing) + forward * (row * Spacing);
                position.y = terrain.Height(position.xz);
                spawned[i] = entityManager.Spawn(prefab, position, rotation, home.Owner);
            }

            // Spawning is structural, so the buffers are fetched once the batch exists.
            var player = FindPlayer(entityManager, home.Owner);
            var command = new PlayerCommand { Type = CommandType.AttackMove, Position = enemy.Position };
            PlayerCommands.Address(ref command, entityManager.GetBuffer<PlayerCommandSubject>(player), spawned);
            entityManager.GetBuffer<PlayerCommand>(player).Add(command);
        }

        private readonly struct Base
        {
            public readonly byte Owner;
            public readonly float3 Position;

            public Base(byte owner, float3 position)
            {
                Owner = owner;
                Position = position;
            }
        }

        // A player's base is where its first building stands; the opponent is the farthest hostile base.
        private static bool TryFindOpposingBases(EntityManager entityManager, out Base first, out Base second)
        {
            first = second = default;
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<BuildingTag, Owner, LocalTransform>()
                .Build(entityManager);
            using var buildings = query.ToEntityArray(Allocator.Temp);
            var lowest = byte.MaxValue;
            foreach (var building in buildings)
            {
                var owner = entityManager.GetComponentData<Owner>(building).Value;
                if (owner != 0 && owner < lowest)
                {
                    lowest = owner;
                    first = new Base(owner, entityManager.GetComponentData<LocalTransform>(building).Position);
                }
            }

            PlayWorld.TryGetSingleton(entityManager, out OwnerRelations relations);
            var farthest = 0f;
            foreach (var building in buildings)
            {
                var owner = entityManager.GetComponentData<Owner>(building).Value;
                var position = entityManager.GetComponentData<LocalTransform>(building).Position;
                var distance = math.distance(first.Position.xz, position.xz);
                if (relations.IsHostile(first.Owner, owner) && distance > farthest)
                {
                    farthest = distance;
                    second = new Base(owner, position);
                }
            }

            return farthest > 0f;
        }

        private static Entity FindPlayer(EntityManager entityManager, byte owner)
        {
            foreach (var player in PlayWorld.Players(entityManager))
            {
                if (entityManager.GetComponentData<Player>(player).Owner == owner)
                {
                    return player;
                }
            }

            return Entity.Null;
        }
    }
}
