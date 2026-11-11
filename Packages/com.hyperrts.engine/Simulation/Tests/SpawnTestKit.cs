using System;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.MatchStart;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Spawns the players, orders and units of a test match.</summary>
    public static class SpawnTestKit
    {
        public static Entity Player(this TestWorld world, byte owner)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(Player));
            using var players = query.ToEntityArray(Allocator.Temp);
            foreach (var player in players)
            {
                if (world.Get<Player>(player).Owner == owner)
                {
                    return player;
                }
            }

            throw new ArgumentException($"No player with owner {owner}.");
        }

        public static void Command(this TestWorld world, byte owner, PlayerCommand command) =>
            world.EntityManager.GetBuffer<PlayerCommand>(world.Player(owner)).Add(command);

        /// <summary>Gives the unit an order through <see cref="OrderWriter"/>, as the command systems do.</summary>
        public static void Issue(this TestWorld world, Entity unit, in Order order, bool queue = false)
        {
            var system = world.World.GetOrCreateSystem<TestOrderSystem>();
            world.World.Unmanaged.GetUnsafeSystemRef<TestOrderSystem>(system)
                .Issue(ref world.World.Unmanaged.ResolveSystemStateRef(system), unit, order, queue);
        }

        public static Entity SpawnUnit(this TestWorld world, byte owner, float3 position, float speed = 5f,
            float radius = 0.5f, float maxHealth = 100f, string name = "Unit")
        {
            var unit = world.CreateGameEntity(owner, position, maxHealth, name, out var writer);
            var agent = new NavAgent { Radius = radius, Mass = 1f };
            agent.WallMargin = agent.HullWallMargin;
            UnitAuthoring.Add(writer, speed, agent);
            return unit;
        }

        /// <summary>Prefabs of these agents, so their movement classes register as a scene's unit prefabs do.</summary>
        public static void AddAgentPrefabs(this TestWorld world, params NavAgent[] agents)
        {
            foreach (var agent in agents)
            {
                var prefab = world.EntityManager.CreateEntity(typeof(Prefab));
                world.EntityManager.AddComponentData(prefab, agent);
            }
        }

        public static Entity SpawnBuilding(this TestWorld world, byte owner, float3 position, float2 footprint,
            bool complete = true, float buildTime = 10f, string name = "Building", int populationProvided = 0,
            float power = 0f)
        {
            var building = world.CreateGameEntity(owner, position, 500f, name, out var writer, buildTime);
            BuildingSetup.Add(writer, footprint, populationProvided, complete, power);
            return building;
        }

        /// <summary>Turns a spawned entity into a prefab, as baking does for referenced GameObjects.</summary>
        public static Entity MakePrefab(this TestWorld world, Entity entity)
        {
            world.EntityManager.AddComponent<Prefab>(entity);
            return entity;
        }

        private static Entity CreateGameEntity(this TestWorld world, byte owner, float3 position,
            float maxHealth, string name, out EntityWriter writer, float buildTime = 5f)
        {
            var entity = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            writer = new EntityWriter(world.EntityManager, entity);
            GameEntitySetup.Add(writer, new GameEntitySpec
            {
                Info = new EntityInfo { TypeId = EntityInfo.TypeIdFromName(name), Name = name },
                Owner = owner,
                MaxHealth = maxHealth,
                VisionRange = 10f,
                EyeHeight = 2f,
                BuildTime = buildTime,
                Population = 1,
                CountsForVictory = true,
            });
            return entity;
        }

        internal static void CreatePlayer(this TestWorld world, byte owner, bool local)
        {
            var player = world.EntityManager.CreateEntity();
            var writer = new EntityWriter(world.EntityManager, player);
            PlayerSetup.Add(writer, owner, $"P{owner}", float4.zero, populationCap: 100);

            if (local)
            {
                world.EntityManager.AddComponent<LocalPlayer>(player);
            }
        }
    }
}
