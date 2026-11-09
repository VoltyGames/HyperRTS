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
    // The players, orders and spawned entities of a test match.
    public sealed partial class TestWorld
    {
        public Entity Player(byte owner)
        {
            using var query = EntityManager.CreateEntityQuery(typeof(Player));
            using var players = query.ToEntityArray(Allocator.Temp);
            foreach (var player in players)
            {
                if (Get<Player>(player).Owner == owner)
                {
                    return player;
                }
            }

            throw new ArgumentException($"No player with owner {owner}.");
        }

        public void Command(byte owner, PlayerCommand command) =>
            EntityManager.GetBuffer<PlayerCommand>(Player(owner)).Add(command);

        /// <summary>Gives the unit an order through <see cref="OrderWriter"/>, as the command systems do.</summary>
        public void Issue(Entity unit, in Order order, bool queue = false)
        {
            var system = World.GetOrCreateSystem<TestOrderSystem>();
            World.Unmanaged.GetUnsafeSystemRef<TestOrderSystem>(system)
                .Issue(ref World.Unmanaged.ResolveSystemStateRef(system), unit, order, queue);
        }

        public Entity SpawnUnit(byte owner, float3 position, float speed = 5f, float radius = 0.5f,
            float maxHealth = 100f, string name = "Unit")
        {
            var unit = CreateGameEntity(owner, position, maxHealth, name, out var writer);
            var agent = new NavAgent { Radius = radius };
            agent.WallMargin = agent.HullWallMargin;
            UnitAuthoring.Add(writer, speed, agent);
            return unit;
        }

        /// <summary>Prefabs of these agents, so their movement classes register as a scene's unit prefabs do.</summary>
        public void AddAgentPrefabs(params NavAgent[] agents)
        {
            foreach (var agent in agents)
            {
                var prefab = EntityManager.CreateEntity(typeof(Prefab));
                EntityManager.AddComponentData(prefab, agent);
            }
        }

        public Entity SpawnBuilding(byte owner, float3 position, float2 footprint, bool complete = true,
            float buildTime = 10f, string name = "Building", int populationProvided = 0, float power = 0f)
        {
            var building = CreateGameEntity(owner, position, 500f, name, out var writer, buildTime);
            BuildingSetup.Add(writer, footprint, populationProvided, complete, power);
            return building;
        }

        /// <summary>Turns a spawned entity into a prefab, as baking does for referenced GameObjects.</summary>
        public Entity MakePrefab(Entity entity)
        {
            EntityManager.AddComponent<Prefab>(entity);
            return entity;
        }

        private Entity CreateGameEntity(byte owner, float3 position, float maxHealth, string name,
            out EntityWriter writer, float buildTime = 5f)
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            writer = new EntityWriter(EntityManager, entity);
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

        private void CreatePlayer(byte owner, bool local)
        {
            var player = EntityManager.CreateEntity();
            var writer = new EntityWriter(EntityManager, player);
            PlayerSetup.Add(writer, owner, $"P{owner}", float4.zero, populationCap: 100);

            if (local)
            {
                EntityManager.AddComponent<LocalPlayer>(player);
            }
        }
    }
}
