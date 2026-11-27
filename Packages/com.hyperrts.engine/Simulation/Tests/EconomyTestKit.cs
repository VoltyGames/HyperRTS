using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Resources.Gathering;
using HyperRTS.Simulation.Resources.Income;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests;

/// <summary>Economy, production and match helpers on top of <see cref="TestWorld"/>.</summary>
public static class EconomyTestKit
{
    private static readonly float3 Far = new(90f, 0f, 90f);

    extension(TestWorld world)
    {
        /// <summary>A resource type that <paramref name="world"/> destroys on dispose.</summary>
        public ResourceType CreateSupplies() =>
            world.Own(ScriptableObject.CreateInstance<ResourceType>());
    }

    extension(TestWorld world)
    {
        /// <summary>A unit prefab parked far from the action, costing <paramref name="cost"/> supplies.</summary>
        public Entity UnitPrefab(ResourceType supplies, int cost, float buildTime = 1f,
            string name = "Unit")
        {
            var prefab = world.MakePrefab(world.SpawnUnit(0, Far, new TestUnit { Name = name }));
            world.SetBuildTime(prefab, buildTime);
            world.SetCost(prefab, supplies, cost);
            return prefab;
        }
    }

    extension(TestWorld world)
    {
        /// <summary>Player 1's producer at the origin, spawning south of it.</summary>
        public Entity SpawnProducer(Entity unitPrefab, bool complete = true) =>
            world.MakeProducer(world.SpawnBuilding(1, float3.zero, new float2(4f, 4f), new TestBuilding
            {
                Complete = complete,
            }),
                new float3(0f, 0f, -4f), unitPrefab);
    }

    extension(TestWorld world)
    {
        public int QueueLength(Entity producer) =>
            world.Buffer<ProductionQueueItem>(producer).Length;
    }

    extension(TestWorld world)
    {
        public Entity SpawnNode(float3 position, ResourceType type, int amount,
            float regrowth = 0f)
        {
            var node = world.EntityManager.CreateEntity();
            world.Add(node, LocalTransform.FromPosition(position));
            var writer = world.Writer(node);
            ResourceNodeAuthoring.Add(writer, type, amount, regrowth);
            return node;
        }
    }

    extension(TestWorld world)
    {
        public Entity MakeHarvester(Entity unit, int capacity, float gatherRate,
            float replacementRange = 30f, float unloadSeconds = 0f)
        {
            var writer = world.Writer(unit);
            HarvesterAuthoring.Add(writer, capacity, gatherRate, replacementRange, unloadSeconds);
            return unit;
        }
    }

    extension(TestWorld world)
    {
        public Entity MakeBuilder(Entity unit, params Entity[] options)
        {
            var writer = world.Writer(unit);
            var buffer = BuilderAuthoring.Add(writer, 1f);
            foreach (var option in options)
            {
                buffer.Add(new BuildOption { Prefab = option });
            }

            return unit;
        }
    }

    extension(TestWorld world)
    {
        public Entity MakeProducer(Entity building, float3 spawnOffset,
            params Entity[] options)
        {
            var writer = world.Writer(building);
            var buffer = ProducerAuthoring.Add(writer, spawnOffset, 5);
            foreach (var option in options)
            {
                buffer.Add(new ProductionOption { Prefab = option });
            }

            return building;
        }
    }

    extension(TestWorld world)
    {
        public Entity SpawnProvider(byte owner, float3 position, int population) =>
            world.SpawnBuilding(owner, position, new float2(2f, 2f), new TestBuilding
            {
                PopulationProvided = population,
            });
    }

    extension(TestWorld world)
    {
        public Entity AddIncome(Entity entity, ResourceType type, int amount,
            float interval)
        {
            var writer = world.Writer(entity);
            writer.Add(new ResourceIncome { Type = type, Amount = amount, Interval = interval });
            return entity;
        }
    }

    extension(TestWorld world)
    {
        public Entity AddBuildLimit(Entity entity, int max)
        {
            var writer = world.Writer(entity);
            writer.Add(new BuildLimit { Max = max });
            return entity;
        }
    }

    extension(TestWorld world)
    {
        /// <summary>Takes <paramref name="entity"/> offline for a game reason, or clears it.</summary>
        public void SetOffline(Entity entity, bool offline)
        {
            var state = world.Get<Offline>(entity);
            state.Set(OfflineReason.Custom, offline);
            world.Set(entity, state);
        }
    }

    extension(TestWorld world)
    {
        public void SetCost(Entity prefab, ResourceType type, int amount) =>
            world.Buffer<ResourceCost>(prefab).Add(new ResourceCost { Type = type, Amount = amount });
    }

    extension(TestWorld world)
    {
        public void AddResources(byte owner, ResourceType type, int amount) =>
            new Stockpile(world.EntityManager.GetBuffer<ResourceStock>(world.Player(owner))).Add(type, amount);
    }

    extension(TestWorld world)
    {
        public int GetResources(byte owner, ResourceType type) =>
            new Stockpile(world.EntityManager.GetBuffer<ResourceStock>(world.Player(owner))).Amount(type);
    }

    extension(TestWorld world)
    {
        public void Order(Entity unit, OrderType type, Entity target) =>
            world.Issue(unit, new Order { Type = type, Target = target });
    }

    extension(TestWorld world)
    {
        public void Produce(byte owner, Entity producer, Entity prefab) =>
            world.Command(owner, new PlayerCommand { Type = CommandType.Produce, Unit = producer, Prefab = prefab });
    }

    extension(TestWorld world)
    {
        public void PlaceBuilding(Entity builder, Entity prefab, float3 position) =>
            world.Command(world.Get<Owner>(builder).Value, new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = position,
                Yaw = PlacementMath.CameraYaw,
            });
    }

    extension(TestWorld world)
    {
        public void PlaceLine(Entity builder, Entity prefab, float3 start, float3 end) =>
            world.PlaceLine(builder, prefab, start, end, new TestLine());
    }

    extension(TestWorld world)
    {
        public void PlaceLine(Entity builder, Entity prefab, float3 start, float3 end,
            TestLine line) =>
            world.Command(world.Get<Owner>(builder).Value, new PlayerCommand
            {
                Type = CommandType.PlaceLine, Unit = builder, Prefab = prefab, Position = start, End = end,
                Argument = (int)line.Join, Queue = line.Queue,
            });
    }

    extension(TestWorld world)
    {
        public void SetBuildTime(Entity entity, float seconds, int population = 1) =>
            world.Set(entity, new Producible { BuildTime = seconds, Population = population });
    }

    extension(TestWorld world)
    {
        /// <summary>Live (non-prefab) entities carrying <typeparamref name="T"/>.</summary>
        public NativeArray<Entity> All<T>() where T : unmanaged, IComponentData
        {
            using var query = world.Query(ComponentType.ReadOnly<T>());
            return query.ToEntityArray(Allocator.Temp);
        }
    }

    extension(TestWorld world)
    {
        /// <summary>Live entities of <paramref name="prefab"/>'s type owned by <paramref name="owner"/>.</summary>
        public List<Entity> Owned(Entity prefab, byte owner) =>
            world.Owned(world.Get<EntityInfo>(prefab).TypeId, owner);
    }

    extension(TestWorld world)
    {
        /// <summary>Live entities of an entity type owned by <paramref name="owner"/>.</summary>
        public List<Entity> Owned(int typeId, byte owner)
        {
            var result = new List<Entity>();
            foreach (var entity in world.All<EntityInfo>())
            {
                var ofType = world.Get<EntityInfo>(entity).TypeId == typeId;
                if (ofType && world.Get<Owner>(entity).Value == owner)
                {
                    result.Add(entity);
                }
            }

            return result;
        }
    }
}
