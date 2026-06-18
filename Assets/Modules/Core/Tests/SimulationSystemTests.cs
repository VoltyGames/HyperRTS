using HyperRTS.Core.Attack;
using HyperRTS.Core.Buildings;
using HyperRTS.Core.Health;
using HyperRTS.Core.Units;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Core.Tests
{
    /// <summary>
    /// Smoke tests for the Burst systems. The world is built from an explicit system
    /// list (not <see cref="DefaultWorldInitialization.Initialize"/>) so global discovery
    /// can't pull unrelated systems into the test world.
    /// </summary>
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

        // Ticks the simulation group with a fixed delta. Updating only this group keeps
        // the pushed time (the whole-world update would let UpdateWorldTimeSystem overwrite it).
        private void Tick(float deltaTime)
        {
            _world.SetTime(new TimeData(elapsedTime: deltaTime, deltaTime: deltaTime));
            _world.GetExistingSystemManaged<SimulationSystemGroup>().Update();
        }

        [Test]
        public void MovementSystem_MovesUnitTowardDestination()
        {
            // Factory spawns the unit at the origin with MovementSpeed = 5.
            var unit = new UnitEntityFactory().CreateEntity(_entityManager);
            _entityManager.AddComponentData(unit, new MoveDestination { Value = new float3(100f, 0f, 0f) });

            Tick(1f);

            var position = _entityManager.GetComponentData<LocalTransform>(unit).Position;
            Assert.AreEqual(5f, position.x, 1e-3f); // speed 5 * dt 1
            Assert.AreEqual(0f, position.z, 1e-3f);
        }

        [Test]
        public void DeathSystem_DestroysEntityAtZeroHealth()
        {
            var entity = _entityManager.CreateEntity(typeof(HealthComponent));
            _entityManager.SetComponentData(entity, new HealthComponent { CurrentHealth = 0, MaxHealth = 100 });

            Tick(1f);

            Assert.IsFalse(_entityManager.Exists(entity), "DeathSystem (runs last) should destroy a zero-health entity.");
        }
    }
}
