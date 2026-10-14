using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Destructible scenery, crushing, remains and passive income.</summary>
    public class DestructionTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity SpawnScenery(float3 position, int crush, Entity remains = default)
        {
            var entity = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            var writer = new EntityManagerWriter(_world.EntityManager, entity);
            DestructibleSetup.AddScenery(ref writer, 50f, crush, remains, float2.zero,
                SceneryId.At(position, quaternion.identity));
            return entity;
        }

        [Test]
        public void HeavyCrusher_FlattensScenery_AndLeavesRemains()
        {
            var remains = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            var fence = SpawnScenery(new float3(5f, 0f, 0f), 1, remains);
            var tank = _world.SpawnUnit(1, new float3(5f, 0f, 0f), radius: 1.5f);
            _world.EntityManager.AddComponentData(tank, new Crusher { Size = 3 });

            _world.Run(0.5f);

            Assert.IsFalse(_world.EntityManager.Exists(fence), "the fence is flattened");
            using var query = _world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Health>());
            Assert.AreEqual(5f, FindNear(query, new float3(5f, 0f, 0f)).x, 0.01f, "its remains lie where it stood");
        }

        [Test]
        public void LightCrusher_LeavesSturdierSceneryStanding()
        {
            var tree = SpawnScenery(new float3(5f, 0f, 0f), 3);
            var jeep = _world.SpawnUnit(1, new float3(5f, 0f, 0f), radius: 1f);
            _world.EntityManager.AddComponentData(jeep, new Crusher { Size = 1 });

            _world.Run(0.5f);

            Assert.IsTrue(_world.EntityManager.Exists(tree));
            Assert.AreEqual(50f, _world.GetHealth(tree));
        }

        [Test]
        public void Splash_HitsNeutralScenery_ButSparesNeutralBuildings()
        {
            var tree = SpawnScenery(new float3(2f, 0f, 0f), 0);
            var house = _world.SpawnBuilding(0, new float3(-2f, 0f, 0f), new float2(2f, 2f));

            _world.QueueDamage(new DamageEvent { Position = float3.zero, SourceFaction = 1, Amount = 20f, Radius = 6f, EdgeFactor = 1f });
            _world.Tick();

            Assert.Less(_world.GetHealth(tree), 50f, "destructible scenery takes the blast");
            Assert.AreEqual(500f, _world.GetHealth(house), "plain neutral buildings don't");
        }

        [Test]
        public void ResourceIncome_PaysOwnerWithFirstOwnerBonus_AndNothingWhileNeutral()
        {
            var supplies = ScriptableObject.CreateInstance<ResourceType>();
            var derrick = _world.SpawnBuilding(0, float3.zero, new float2(4f, 4f));
            var writer = new EntityManagerWriter(_world.EntityManager, derrick);
            writer.Add(new ResourceIncome { Type = supplies, Amount = 10, Interval = 5f, FirstOwnerBonus = 100 });

            _world.Run(6f);
            Assert.AreEqual(0, _world.GetResources(1, supplies), "a neutral derrick pays no one");

            _world.EntityManager.SetComponentData(derrick, new Faction { Value = 1 });
            _world.Run(11f);
            Assert.AreEqual(120, _world.GetResources(1, supplies), "the bonus plus two payments");
            Object.DestroyImmediate(supplies);
        }

        private float3 FindNear(EntityQuery query, float3 point)
        {
            using var transforms = query.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
            foreach (var transform in transforms)
            {
                if (math.distance(transform.Position, point) < 0.1f)
                {
                    return transform.Position;
                }
            }

            return new float3(float.NaN);
        }
    }
}
