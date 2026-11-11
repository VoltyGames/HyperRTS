using HyperRTS.Editor.Common;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>Play-mode cheats applied straight to the authoritative world; editor only, never part of the game.</summary>
    internal static class MatchCheats
    {
        // Larger than any health, so one untyped hit kills through armour.
        private const float LethalDamage = 1e9f;

        /// <summary>Hands control of a player to you; its AI stops so the two don't fight over it. Single player only.</summary>
        public static void MakeLocal(EntityManager entityManager, Entity player)
        {
            foreach (var other in PlayWorld.Players(entityManager))
            {
                entityManager.RemoveComponent<LocalPlayer>(other);
            }

            entityManager.AddComponent<LocalPlayer>(player);
            entityManager.RemoveComponent<AIPlayer>(player);
        }

        public static void AddResources(EntityManager entityManager, Entity player, int amount)
        {
            var stock = entityManager.GetBuffer<ResourceStock>(player);
            foreach (var type in EditorAssets.FindAssets<ResourceType>())
            {
                ResourceMath.Add(stock, type, amount);
            }
        }

        public static bool FogEnabled(EntityManager entityManager) =>
            PlayWorld.TryGetSingleton(entityManager, out MapSettings map) && map.FogOfWar;

        public static void SetFog(EntityManager entityManager, bool enabled)
        {
            using var query = entityManager.CreateEntityQuery(typeof(MapSettings));
            if (!query.TryGetSingleton(out MapSettings map))
            {
                return;
            }

            map.FogOfWar = enabled;
            query.SetSingleton(map);
        }

        /// <summary>
        /// Finishes every construction site and the current production of one owner, through the construction and
        /// production systems next frame, so completion events, sounds and stats still happen.
        /// </summary>
        public static void InstantBuild(EntityManager entityManager, byte owner)
        {
            using var sites = entityManager.CreateEntityQuery(typeof(ConstructionProgress), typeof(Owner));
            foreach (var site in sites.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.GetComponentData<Owner>(site).Value == owner)
                {
                    entityManager.SetComponentData(site, new ConstructionProgress { Value = 1f });
                }
            }

            using var producers = entityManager.CreateEntityQuery(typeof(Producer), typeof(ProductionQueueItem), typeof(Owner));
            foreach (var entity in producers.ToEntityArray(Allocator.Temp))
            {
                var owned = entityManager.GetComponentData<Owner>(entity).Value == owner;
                if (!owned || entityManager.GetBuffer<ProductionQueueItem>(entity, true).Length == 0)
                {
                    continue;
                }

                var producer = entityManager.GetComponentData<Producer>(entity);
                producer.Elapsed = float.MaxValue;
                entityManager.SetComponentData(entity, producer);
            }
        }

        /// <summary>
        /// The local player's selected entities with health, or none when the selection lives in another world than
        /// the cheats write to (a client of a dedicated server).
        /// </summary>
        public static NativeArray<Entity> Selection(EntityManager entityManager)
        {
            var presented = PlayWorld.TryGetPresented(out var view) && view.World == entityManager.World;
            if (!presented)
            {
                return new NativeArray<Entity>(0, Allocator.Temp);
            }

            using var query = entityManager.CreateEntityQuery(typeof(Selected), typeof(Health));
            return query.ToEntityArray(Allocator.Temp);
        }

        /// <summary>Heals <paramref name="units"/> to full through the damage system.</summary>
        public static void Repair(EntityManager entityManager, NativeArray<Entity> units) =>
            Hit(entityManager, units, heal: true);

        /// <summary>Kills <paramref name="units"/> through the damage system, so deaths play out as in a fight.</summary>
        public static void Destroy(EntityManager entityManager, NativeArray<Entity> units) =>
            Hit(entityManager, units, heal: false);

        // A heal is a negative hit of the unit's full health.
        private static void Hit(EntityManager entityManager, NativeArray<Entity> units, bool heal)
        {
            using var queue = entityManager.CreateEntityQuery(typeof(DamageQueue));
            if (!queue.TryGetSingletonEntity<DamageQueue>(out var events))
            {
                return;
            }

            var hits = entityManager.GetBuffer<DamageEvent>(events);
            foreach (var unit in units)
            {
                var amount = heal ? -entityManager.GetComponentData<Health>(unit).Max : LethalDamage;
                var position = entityManager.GetComponentData<LocalTransform>(unit).Position;
                hits.Add(DamageEvent.Direct(unit, position, Entity.Null, Owner.Neutral, amount));
            }
        }
    }
}
