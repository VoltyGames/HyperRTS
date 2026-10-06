using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Economy, production and match helpers on top of <see cref="TestWorld"/>.</summary>
    public static class EconomyTestKit
    {
        public static Entity SpawnNode(this TestWorld world, float3 position, ResourceType type, int amount,
            float regrowth = 0f)
        {
            var node = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(node, LocalTransform.FromPosition(position));
            var sink = new EntityManagerSink(world.EntityManager, node);
            ResourceNodeSetup.Add(ref sink, type, amount, regrowth);
            return node;
        }

        public static Entity MakeHarvester(this TestWorld world, Entity unit, int capacity, float gatherRate)
        {
            var sink = new EntityManagerSink(world.EntityManager, unit);
            HarvesterSetup.Add(ref sink, capacity, gatherRate);
            return unit;
        }

        public static Entity MakeBuilder(this TestWorld world, Entity unit, params Entity[] options)
        {
            var sink = new EntityManagerSink(world.EntityManager, unit);
            var buffer = BuilderSetup.Add(ref sink, 1f);
            foreach (var option in options)
            {
                buffer.Add(new BuildOption { Prefab = option });
            }

            return unit;
        }

        public static Entity MakeProducer(this TestWorld world, Entity building, float3 spawnOffset,
            params Entity[] options)
        {
            var sink = new EntityManagerSink(world.EntityManager, building);
            var buffer = ProducerSetup.Add(ref sink, spawnOffset, 5);
            foreach (var option in options)
            {
                buffer.Add(new ProductionOption { Prefab = option });
            }

            return building;
        }

        public static Entity SpawnProvider(this TestWorld world, byte faction, float3 position, int population) =>
            world.SpawnBuilding(faction, position, new float2(2f, 2f), populationProvided: population);

        public static void SetCost(this TestWorld world, Entity prefab, ResourceType type, int amount) =>
            world.EntityManager.GetBuffer<ResourceCost>(prefab).Add(new ResourceCost { Type = type, Amount = amount });

        public static void Give(this TestWorld world, byte faction, ResourceType type, int amount) =>
            ResourceMath.Add(world.EntityManager.GetBuffer<ResourceStock>(world.Player(faction)), type, amount);

        public static int Stock(this TestWorld world, byte faction, ResourceType type) =>
            ResourceMath.GetAmount(world.EntityManager.GetBuffer<ResourceStock>(world.Player(faction)), type);

        public static void Order(this TestWorld world, Entity unit, OrderType type, Entity target)
        {
            world.EntityManager.SetComponentData(unit, new ActiveOrder { Value = new Order { Type = type, Target = target } });
            world.EntityManager.SetComponentEnabled<ActiveOrder>(unit, true);
        }

        public static void Produce(this TestWorld world, byte faction, Entity producer, Entity prefab) =>
            world.Command(faction, new PlayerCommand { Type = CommandType.Produce, Unit = producer, Prefab = prefab });

        public static void SetBuildTime(this TestWorld world, Entity entity, float seconds, int population = 1) =>
            world.EntityManager.SetComponentData(entity, new Producible { BuildTime = seconds, Population = population });

        /// <summary>Live (non-prefab) entities carrying <typeparamref name="T"/>.</summary>
        public static NativeArray<Entity> All<T>(this TestWorld world) where T : unmanaged, IComponentData
        {
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.ToEntityArray(Allocator.Temp);
        }
    }
}
