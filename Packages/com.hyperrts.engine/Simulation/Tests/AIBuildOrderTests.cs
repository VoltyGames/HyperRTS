using System.Collections.Generic;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>AI build orders: buildings placed by builders, units queued, counts respected.</summary>
    public class AIBuildOrderTests
    {
        private const float BuildingGap = 2f;

        private TestWorld _world;
        private ResourceType _supplies;
        private Entity _soldier;
        private Entity _barracks;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = ScriptableObject.CreateInstance<ResourceType>();
            var sink = new EntityManagerSink(_world.EntityManager, _world.Player(2));
            AIPlayerSetup.Add(ref sink, new AIPlayer { ThinkInterval = 0.5f, AttackWaveSize = 50, BuildingGap = BuildingGap });

            _world.SpawnBuilding(2, float3.zero, new float2(4f, 4f), name: "HQ", populationProvided: 20);
            _soldier = _world.MakePrefab(_world.SpawnUnit(0, new float3(90f, 0f, 90f), name: "Soldier"));
            _world.SetBuildTime(_soldier, 0.5f);
            _world.SetCost(_soldier, _supplies, 10);
            _barracks = _world.MakePrefab(_world.SpawnBuilding(0, float3.zero, new float2(4f, 4f), complete: false,
                buildTime: 1f, name: "Barracks"));
            _world.SetCost(_barracks, _supplies, 50);
            _world.MakeProducer(_barracks, new float3(0f, 0f, -3f), _soldier);
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            Object.DestroyImmediate(_supplies);
        }

        private Entity SpawnBuilder(float3 position) =>
            _world.MakeBuilder(_world.SpawnUnit(2, position, name: "Builder"), _barracks);

        private void SetBuildOrder(params (Entity Prefab, int Count)[] steps)
        {
            var buffer = _world.EntityManager.GetBuffer<AIBuildStep>(_world.Player(2));
            foreach (var step in steps)
            {
                buffer.Add(new AIBuildStep { Prefab = step.Prefab, Count = step.Count });
            }
        }

        private List<Entity> Owned(string name)
        {
            var result = new List<Entity>();
            var typeId = EntityInfo.TypeIdFromName(name);
            foreach (var entity in _world.All<EntityInfo>())
            {
                if (_world.Get<EntityInfo>(entity).TypeId == typeId && _world.Get<Faction>(entity).Value == 2)
                {
                    result.Add(entity);
                }
            }

            return result;
        }

        [Test]
        public void BuildOrder_PlacesBuildingWithBuilder_ThenTrainsUnit()
        {
            SpawnBuilder(new float3(0f, 0f, -6f));
            SetBuildOrder((_barracks, 1), (_soldier, 1));
            _world.Give(2, _supplies, 100);

            _world.Run(6f);

            var barracks = Owned("Barracks");
            Assert.AreEqual(1, barracks.Count, "one barracks placed");
            Assert.IsFalse(_world.IsEnabled<ConstructionProgress>(barracks[0]), "the builder finished it");
            var offset = math.abs(_world.Get<LocalTransform>(barracks[0]).Position.xz);
            Assert.GreaterOrEqual(math.cmax(offset) - 4f, BuildingGap - 1e-3f, "a lane is left next to the HQ");
            Assert.GreaterOrEqual(Owned("Soldier").Count, 1, "then a soldier was trained there");
        }

        [Test]
        public void BuildOrder_CountsSitesAndQueuedUnits_AndNeverOverbuilds()
        {
            SpawnBuilder(new float3(0f, 0f, -6f));
            SpawnBuilder(new float3(6f, 0f, -6f));
            var locked = _world.MakePrefab(_world.SpawnBuilding(0, float3.zero, new float2(2f, 2f), name: "Locked"));
            _world.EntityManager.GetBuffer<Prerequisite>(locked)
                .Add(new Prerequisite { TypeId = EntityInfo.TypeIdFromName("Never Built") });
            SetBuildOrder((_barracks, 2), (_soldier, 3), (locked, 1));
            _world.Give(2, _supplies, 1000);

            _world.Run(12f);

            Assert.AreEqual(2, Owned("Barracks").Count);
            Assert.AreEqual(3, Owned("Soldier").Count, "the unmet locked step keeps the AI from training freely");
            Assert.AreEqual(1000 - 2 * 50 - 3 * 10, _world.Stock(2, _supplies));
        }

        [Test]
        public void BuildOrder_SkipsUnaffordableStep_UntilItCanPay()
        {
            SpawnBuilder(new float3(0f, 0f, -6f));
            var existing = _world.SpawnBuilding(2, new float3(20f, 0f, 20f), new float2(4f, 4f), name: "Barracks");
            _world.MakeProducer(existing, new float3(0f, 0f, -3f), _soldier);
            SetBuildOrder((_barracks, 2), (_soldier, 1));
            _world.Give(2, _supplies, 20);

            _world.Run(2f);

            Assert.AreEqual(1, Owned("Barracks").Count, "too poor for the second barracks");
            Assert.AreEqual(1, Owned("Soldier").Count, "so it trained the affordable next step");
        }
    }
}
