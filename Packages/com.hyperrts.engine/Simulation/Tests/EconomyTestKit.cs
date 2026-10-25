using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Economy, production and match helpers on top of <see cref="TestWorld"/>.</summary>
    public static class EconomyTestKit
    {
        private static readonly float3 Far = new(90f, 0f, 90f);

        /// <summary>A resource type that <paramref name="world"/> destroys on dispose.</summary>
        public static ResourceType CreateSupplies(this TestWorld world) =>
            world.Own(ScriptableObject.CreateInstance<ResourceType>());

        /// <summary>A unit prefab parked far from the action, costing <paramref name="cost"/> supplies.</summary>
        public static Entity UnitPrefab(this TestWorld world, ResourceType supplies, int cost, float buildTime = 1f,
            string name = "Unit")
        {
            var prefab = world.MakePrefab(world.SpawnUnit(0, Far, name: name));
            world.SetBuildTime(prefab, buildTime);
            world.SetCost(prefab, supplies, cost);
            return prefab;
        }

        /// <summary>Player 1's producer at the origin, spawning south of it.</summary>
        public static Entity SpawnProducer(this TestWorld world, Entity unitPrefab, bool complete = true) =>
            world.MakeProducer(world.SpawnBuilding(1, float3.zero, new float2(4f, 4f), complete),
                new float3(0f, 0f, -4f), unitPrefab);

        public static int QueueLength(this TestWorld world, Entity producer) =>
            world.EntityManager.GetBuffer<ProductionQueueItem>(producer).Length;

        public static Entity SpawnNode(this TestWorld world, float3 position, ResourceType type, int amount,
            float regrowth = 0f)
        {
            var node = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(node, LocalTransform.FromPosition(position));
            var writer = new EntityManagerWriter(world.EntityManager, node);
            ResourceNodeAuthoring.Add(ref writer, type, amount, regrowth);
            return node;
        }

        public static Entity MakeHarvester(this TestWorld world, Entity unit, int capacity, float gatherRate,
            float replacementRange = 30f)
        {
            var writer = new EntityManagerWriter(world.EntityManager, unit);
            HarvesterAuthoring.Add(ref writer, capacity, gatherRate, replacementRange);
            return unit;
        }

        public static Entity MakeBuilder(this TestWorld world, Entity unit, params Entity[] options)
        {
            var writer = new EntityManagerWriter(world.EntityManager, unit);
            var buffer = BuilderAuthoring.Add(ref writer, 1f);
            foreach (var option in options)
            {
                buffer.Add(new BuildOption { Prefab = option });
            }

            return unit;
        }

        /// <summary>Adds second-page options to a builder made by <see cref="MakeBuilder"/>.</summary>
        public static Entity AddAdvancedBuildOptions(this TestWorld world, Entity builder, params Entity[] options)
        {
            var buffer = world.EntityManager.GetBuffer<BuildOption>(builder);
            foreach (var option in options)
            {
                buffer.Add(new BuildOption { Prefab = option, Advanced = true });
            }

            return builder;
        }

        public static Entity MakeProducer(this TestWorld world, Entity building, float3 spawnOffset,
            params Entity[] options)
        {
            var writer = new EntityManagerWriter(world.EntityManager, building);
            var buffer = ProducerAuthoring.Add(ref writer, spawnOffset, 5);
            foreach (var option in options)
            {
                buffer.Add(new ProductionOption { Prefab = option });
            }

            return building;
        }

        public static Entity SpawnProvider(this TestWorld world, byte faction, float3 position, int population) =>
            world.SpawnBuilding(faction, position, new float2(2f, 2f), populationProvided: population);

        public static Entity AddIncome(this TestWorld world, Entity entity, ResourceType type, int amount,
            float interval)
        {
            var writer = new EntityManagerWriter(world.EntityManager, entity);
            writer.Add(new ResourceIncome { Type = type, Amount = amount, Interval = interval });
            return entity;
        }

        public static Entity AddBuildLimit(this TestWorld world, Entity entity, int max)
        {
            var writer = new EntityManagerWriter(world.EntityManager, entity);
            writer.Add(new BuildLimit { Max = max });
            return entity;
        }

        /// <summary>Takes <paramref name="entity"/> offline for a game reason, or clears it.</summary>
        public static void SetOffline(this TestWorld world, Entity entity, bool offline)
        {
            var state = world.Get<Offline>(entity);
            state.Set(OfflineReason.Custom, offline);
            world.EntityManager.SetComponentData(entity, state);
        }

        public static void SetCost(this TestWorld world, Entity prefab, ResourceType type, int amount) =>
            world.EntityManager.GetBuffer<ResourceCost>(prefab).Add(new ResourceCost { Type = type, Amount = amount });

        public static void AddResources(this TestWorld world, byte faction, ResourceType type, int amount) =>
            ResourceMath.Add(world.EntityManager.GetBuffer<ResourceStock>(world.Player(faction)), type, amount);

        public static int GetResources(this TestWorld world, byte faction, ResourceType type) =>
            ResourceMath.GetAmount(world.EntityManager.GetBuffer<ResourceStock>(world.Player(faction)), type);

        public static void Order(this TestWorld world, Entity unit, OrderType type, Entity target) =>
            world.Issue(unit, new Order { Type = type, Target = target });

        public static void Produce(this TestWorld world, byte faction, Entity producer, Entity prefab) =>
            world.Command(faction, new PlayerCommand { Type = CommandType.Produce, Unit = producer, Prefab = prefab });

        public static void PlaceBuilding(this TestWorld world, Entity builder, Entity prefab, float3 position) =>
            world.Command(world.Get<Faction>(builder).Value, new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = position,
                Yaw = PlacementMath.CameraYaw,
            });

        public static void PlaceLine(this TestWorld world, Entity builder, Entity prefab, float3 start, float3 end) =>
            world.Command(world.Get<Faction>(builder).Value, new PlayerCommand
            {
                Type = CommandType.PlaceLine, Unit = builder, Prefab = prefab, Position = start, End = end,
            });

        public static void SetBuildTime(this TestWorld world, Entity entity, float seconds, int population = 1) =>
            world.EntityManager.SetComponentData(entity, new Producible { BuildTime = seconds, Population = population });

        /// <summary>Live (non-prefab) entities carrying <typeparamref name="T"/>.</summary>
        public static NativeArray<Entity> All<T>(this TestWorld world) where T : unmanaged, IComponentData
        {
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.ToEntityArray(Allocator.Temp);
        }

        /// <summary>Live entities of <paramref name="prefab"/>'s type owned by <paramref name="faction"/>.</summary>
        public static List<Entity> Owned(this TestWorld world, Entity prefab, byte faction) =>
            world.Owned(world.Get<EntityInfo>(prefab).TypeId, faction);

        /// <summary>Live entities of an entity type owned by <paramref name="faction"/>.</summary>
        public static List<Entity> Owned(this TestWorld world, int typeId, byte faction)
        {
            var result = new List<Entity>();
            foreach (var entity in world.All<EntityInfo>())
            {
                var ofType = world.Get<EntityInfo>(entity).TypeId == typeId;
                if (ofType && world.Get<Faction>(entity).Value == faction)
                {
                    result.Add(entity);
                }
            }

            return result;
        }
    }
}
