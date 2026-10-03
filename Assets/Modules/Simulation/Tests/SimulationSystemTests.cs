using HyperRTS.Core;
using HyperRTS.Simulation.Attack;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Health;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Built from an explicit system list so global discovery can't add unrelated systems.</summary>
    public class SimulationSystemTests
    {
        private World _world;
        private EntityManager _entityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("HyperRTS Test World");
            _entityManager = _world.EntityManager;

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(_world,
                typeof(SimulationSystemGroup),
                typeof(TransformSystemGroup), // MovementSystemGroup orders against it
                typeof(EndSimulationEntityCommandBufferSystem),
                typeof(OrderSystemGroup),
                typeof(MovementSystemGroup),
                typeof(CombatSystemGroup),
                typeof(ProductionSystemGroup),
                typeof(LifecycleSystemGroup),
                typeof(MovementSystem),
                typeof(AttackSystem),
                typeof(ConstructionSystem),
                typeof(DeathSystem));
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated)
            {
                _world.Dispose();
            }

            _world = null;
        }

        // Updates only this group so UpdateWorldTimeSystem doesn't overwrite the pushed time.
        private void Tick(float deltaTime)
        {
            _world.SetTime(new TimeData(elapsedTime: deltaTime, deltaTime: deltaTime));
            _world.GetExistingSystemManaged<SimulationSystemGroup>().Update();
        }

        private void Order(Entity unit, float3 destination)
        {
            _entityManager.SetComponentData(unit, new MoveDestination { Value = destination });
            _entityManager.SetComponentEnabled<MoveDestination>(unit, true);
        }

        [Test]
        public void MovementSystem_MovesUnitTowardDestination()
        {
            // Spawns at the origin with speed 5.
            var unit = new UnitEntityFactory().CreateEntity(_entityManager);
            Order(unit, new float3(100f, 0f, 0f));

            Tick(1f);

            var position = _entityManager.GetComponentData<LocalTransform>(unit).Position;
            Assert.AreEqual(5f, position.x, 1e-3f); // speed 5 * dt 1
            Assert.AreEqual(0f, position.z, 1e-3f);
        }

        [Test]
        public void MovementSystem_IdleUnitDoesNotMove()
        {
            var unit = new UnitEntityFactory().CreateEntity(_entityManager);
            _entityManager.SetComponentData(unit, new MoveDestination { Value = new float3(100f, 0f, 0f) });

            Tick(1f);

            Assert.AreEqual(float3.zero, _entityManager.GetComponentData<LocalTransform>(unit).Position,
                "A disabled MoveDestination is not an order.");
        }

        [Test]
        public void MovementSystem_ArrivalDisablesOrderWithoutStructuralChange()
        {
            var unit = new UnitEntityFactory().CreateEntity(_entityManager);
            var archetype = _entityManager.GetChunk(unit).Archetype;
            Order(unit, new float3(2f, 0f, 0f));

            Tick(1f); // reaches the destination (step clamps to distance)
            Tick(1f); // within threshold -> order cleared

            Assert.IsFalse(_entityManager.IsComponentEnabled<MoveDestination>(unit));
            Assert.AreEqual(archetype, _entityManager.GetChunk(unit).Archetype);
        }

        [Test]
        public void ConstructionSystem_CompletesAndDisablesProgress()
        {
            var building = new BuildingEntityFactory().CreateEntity(_entityManager);

            Tick(5f);
            Assert.AreEqual(0.5f, _entityManager.GetComponentData<ConstructionProgress>(building).Value, 1e-3f);
            Assert.IsTrue(_entityManager.IsComponentEnabled<ConstructionProgress>(building));

            Tick(6f);
            Assert.AreEqual(1f, _entityManager.GetComponentData<ConstructionProgress>(building).Value, 1e-3f);
            Assert.IsFalse(_entityManager.IsComponentEnabled<ConstructionProgress>(building), "complete");
        }

        [Test]
        public void AttackSystem_DealsDamageOnCooldown()
        {
            var target = _entityManager.CreateEntity(typeof(HealthComponent));
            _entityManager.SetComponentData(target, new HealthComponent { CurrentHealth = 100, MaxHealth = 100 });

            var attacker = _entityManager.CreateEntity();
            _entityManager.AddComponentData(attacker, new AttackTarget { Value = target });
            _entityManager.AddComponentData(attacker, new MeleeAttackDamage { Value = 10 });
            _entityManager.AddComponentData(attacker, new AttackCooldown { Interval = 1f, TimeRemaining = 0f });

            Tick(0.1f); // ready -> hit
            Assert.AreEqual(90, _entityManager.GetComponentData<HealthComponent>(target).CurrentHealth);

            Tick(0.5f); // cooling down
            Assert.AreEqual(90, _entityManager.GetComponentData<HealthComponent>(target).CurrentHealth);

            Tick(0.6f); // cooldown elapsed -> hit
            Assert.AreEqual(80, _entityManager.GetComponentData<HealthComponent>(target).CurrentHealth);
        }

        [Test]
        public void DeathSystem_DestroysEntityAtZeroHealth()
        {
            var entity = _entityManager.CreateEntity(typeof(HealthComponent));
            _entityManager.SetComponentData(entity, new HealthComponent { CurrentHealth = 0, MaxHealth = 100 });

            Tick(1f);

            Assert.IsFalse(_entityManager.Exists(entity), "DeathSystem (runs last) should destroy a zero-health entity.");
        }

        [Test]
        public void Factory_ReturnsRealEntityAtRecordTime()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var unit = new UnitEntityFactory().CreateEntity(ecb);
            Assert.GreaterOrEqual(unit.Index, 0, "Entities 6.6 command buffers return real entities, not placeholders.");

            ecb.Playback(_entityManager);
            ecb.Dispose();

            Assert.IsTrue(_entityManager.Exists(unit));
            Assert.IsTrue(_entityManager.HasComponent<UnitTag>(unit));
        }
    }
}
