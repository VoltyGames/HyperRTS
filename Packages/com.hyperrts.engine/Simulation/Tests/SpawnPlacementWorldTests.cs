using System.Collections.Generic;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Each spawn path's root has the world pose its placement implies on the tick it appears.</summary>
    public class SpawnPlacementWorldTests
    {
        private static readonly float3 Far = new(90f, 0f, 90f);

        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = ScriptableObject.CreateInstance<ResourceType>();
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_supplies);
        }

        [Test]
        public void ProducedUnit_KeepsThePrefabScale_FacingLikeItsProducer()
        {
            var prefab = Scaled(_world.MakePrefab(_world.SpawnUnit(0, Far)), 1.5f);
            _world.SetBuildTime(prefab, 0.2f);
            _world.SetCost(prefab, _supplies, 10);
            var producer = _world.MakeProducer(_world.SpawnBuilding(1, float3.zero, new float2(4f, 4f)),
                new float3(0f, 0f, -4f), prefab);
            var facing = quaternion.RotateY(0.7f);
            _world.EntityManager.SetComponentData(producer, LocalTransform.FromPositionRotation(float3.zero, facing));
            _world.SpawnProvider(1, new float3(20f, 0f, 20f), 10);
            _world.AddResources(1, _supplies, 100);

            _world.Produce(1, producer, prefab);
            var unit = Single(TickUntilSpawned(60, ComponentType.ReadOnly<UnitTag>()));

            var placed = _world.Get<LocalTransform>(unit);
            Assert.AreEqual(1.5f, placed.Scale, 1e-5f);
            Assert.Greater(math.abs(math.dot(placed.Rotation.value, facing.value)), 0.9999f, "faces like its producer");
            AssertPlaced(unit, float4x4.identity);
        }

        [Test]
        public void PlacedSite_KeepsThePrefabScale()
        {
            var prefab = _world.MakePrefab(_world.SpawnBuilding(0, Far, new float2(4f, 4f), name: "Barracks"));
            Scaled(prefab, 1.25f);
            _world.SetCost(prefab, _supplies, 10);
            var builder = _world.MakeBuilder(_world.SpawnUnit(1, float3.zero), prefab);
            _world.AddResources(1, _supplies, 100);

            _world.PlaceBuilding(builder, prefab, new float3(8f, 0f, 0f));
            var site = Single(TickUntilSpawned(5, ComponentType.ReadOnly<BuildingTag>()));

            Assert.AreEqual(1.25f, _world.Get<LocalTransform>(site).Scale, 1e-5f);
            AssertPlaced(site, float4x4.identity);
        }

        [Test]
        public void Projectile_KeepsThePrefabScale_AimedAtItsTarget()
        {
            var prefab = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(prefab, LocalTransform.FromScale(2f));
            _world.MakePrefab(prefab);
            _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 8f, cooldown: 100f, projectile: prefab);
            _world.SpawnUnit(2, new float3(4f, 0f, 4f), speed: 0f);

            var projectile = Single(TickUntilSpawned(30, ComponentType.ReadOnly<Projectile>()));

            var placed = _world.Get<LocalTransform>(projectile);
            Assert.AreEqual(2f, placed.Scale, 1e-5f);
            Assert.Greater(math.abs(placed.Rotation.value.y), 0.1f, "turned toward the target");
            AssertPlaced(projectile, float4x4.identity);
        }

        [Test]
        public void Remains_CarryTheDeadPiecesPoseAndStretch()
        {
            var remains = _world.MakePrefab(_world.EntityManager.CreateEntity(typeof(LocalTransform)));
            var pose = LocalTransform.FromPositionRotationScale(new float3(5f, 0f, 3f), quaternion.RotateY(0.6f), 1.3f);
            var stretch = float4x4.Scale(1f, 2.5f, 0.8f);
            var piece = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(piece, pose);
            _world.EntityManager.AddComponentData(piece, new PostTransformMatrix { Value = stretch });
            var writer = new EntityManagerWriter(_world.EntityManager, piece);
            DestructibleSetup.AddScenery(ref writer, 50f, 0, remains, float2.zero, SceneryId.At(pose.Position,
                pose.Rotation));

            _world.QueueDamage(new DamageEvent
            {
                Position = pose.Position, SourceFaction = 1, Amount = 100f, Radius = 2f, EdgeFactor = 1f,
            });
            var spawned = Single(TickUntilSpawned(5, ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Health>()));

            Assert.AreEqual(pose, _world.Get<LocalTransform>(spawned));
            AssertMatrix(stretch, _world.Get<PostTransformMatrix>(spawned).Value, "the stretch is kept");
            AssertPlaced(spawned, stretch);
        }

        [Test]
        public void DeliveryCarriers_KeepThePrefabScale_HeadingForTheSource()
        {
            var carrier = Scaled(_world.MakePrefab(_world.SpawnUnit(0, float3.zero, speed: 20f, name: "Truck")), 1.2f);
            var source = _world.SpawnBuilding(1, new float3(40f, 0f, 0f), new float2(4f, 4f));
            var writer = new EntityManagerWriter(_world.EntityManager, source);
            writer.Add(new DeliverySource
            {
                Carrier = carrier, Count = 2, Interval = 1f, Type = _supplies, Amount = 50, ArrivalRange = 6f,
                Spacing = 4f,
            });

            var carriers = TickUntilSpawned(60, ComponentType.ReadOnly<DeliveryCarrier>());

            Assert.AreEqual(2, carriers.Count);
            foreach (var spawned in carriers)
            {
                Assert.AreEqual(1.2f, _world.Get<LocalTransform>(spawned).Scale, 1e-5f);
                AssertPlaced(spawned, float4x4.identity);
            }
        }

        [Test]
        public void AbilitySpawn_KeepsThePrefabRotationAndScale_AtTheTarget()
        {
            var pose = LocalTransform.FromPositionRotationScale(Far, quaternion.RotateY(0.9f), 1.4f);
            var beacon = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(beacon, pose);
            _world.MakePrefab(beacon);
            var strike = new Ability
            {
                Id = EntityInfo.TypeIdFromName("Strike"), Name = "Strike", Target = AbilityTarget.Point,
                Filter = AbilityTargetFilter.Hostile, Cooldown = 2f, SpawnPrefab = beacon,
            };
            var silo = _world.SpawnBuilding(1, float3.zero, new float2(4f, 4f));
            var writer = new EntityManagerWriter(_world.EntityManager, silo);
            writer.AddBuffer(new[] { strike });

            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.UseAbility, Unit = silo, Position = new float3(60f, 0f, 60f), Argument = strike.Id,
            });
            var spawned = Single(TickUntilSpawned(5, ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<Faction>(), ComponentType.Exclude<EntityInfo>()));

            var placed = _world.Get<LocalTransform>(spawned);
            Assert.AreEqual(new float3(60f, 0f, 60f), placed.Position);
            Assert.AreEqual(pose.Rotation, placed.Rotation);
            Assert.AreEqual(pose.Scale, placed.Scale);
            AssertPlaced(spawned, float4x4.identity);
        }

        private Entity Scaled(Entity prefab, float scale)
        {
            var transform = _world.Get<LocalTransform>(prefab);
            transform.Scale = scale;
            _world.EntityManager.SetComponentData(prefab, transform);
            return prefab;
        }

        /// <summary>Ticks one frame at a time so the new entities are read on the tick they appear.</summary>
        private List<Entity> TickUntilSpawned(int maxFrames, params ComponentType[] types)
        {
            using var query = _world.EntityManager.CreateEntityQuery(types);
            using var existing = query.ToEntityArray(Allocator.Temp);
            var before = new HashSet<Entity>(existing.ToArray());
            for (var frame = 0; frame < maxFrames; frame++)
            {
                _world.Tick();
                using var now = query.ToEntityArray(Allocator.Temp);
                var spawned = new List<Entity>();
                foreach (var entity in now)
                {
                    if (!before.Contains(entity))
                    {
                        spawned.Add(entity);
                    }
                }

                if (spawned.Count > 0)
                {
                    return spawned;
                }
            }

            Assert.Fail($"Nothing spawned within {maxFrames} frames.");
            return null;
        }

        private static Entity Single(List<Entity> spawned)
        {
            Assert.AreEqual(1, spawned.Count, "one entity spawned");
            return spawned[0];
        }

        private void AssertPlaced(Entity spawned, float4x4 stretch)
        {
            var expected = math.mul(_world.Get<LocalTransform>(spawned).ToMatrix(), stretch);
            AssertMatrix(expected, _world.Get<LocalToWorld>(spawned).Value, "LocalToWorld");
        }

        private static void AssertMatrix(float4x4 expected, float4x4 actual, string what)
        {
            for (var c = 0; c < 4; c++)
            {
                Assert.That(math.distance(expected[c], actual[c]), Is.LessThan(1e-4f), $"{what} column {c}");
            }
        }
    }
}
