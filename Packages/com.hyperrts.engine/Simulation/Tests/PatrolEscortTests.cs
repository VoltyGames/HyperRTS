using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Patrol loops between two points fighting on the way; escort follows a friendly unit and defends it.</summary>
    public class PatrolEscortTests
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

        [Test]
        public void Patrol_LoopsBetweenStartAndTarget()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            Patrol(unit, new float3(10f, 0f, 0f));

            var legs = 0;
            var goingOut = true;
            for (var t = 0f; t < 10f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var x = _world.PositionOf(unit).x;
                if (goingOut ? x > 9.5f : x < 0.5f)
                {
                    legs++;
                    goingOut = !goingOut;
                }
            }

            Assert.GreaterOrEqual(legs, 3, "out, back and out again");
            Assert.IsTrue(_world.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(OrderType.Patrol, _world.Get<ActiveOrder>(unit).Value.Type);
        }

        [Test]
        public void QueuedPatrol_LoopsBetweenTheEndOfTheQueueAndTheNewPoint()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            _world.MoveTo(unit, new float3(10f, 0f, 0f));
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.Patrol, Unit = unit, Position = new float3(10f, 0f, 10f), Queue = true,
            });

            var legs = 0;
            var goingOut = true;
            for (var t = 0f; t < 14f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var position = _world.PositionOf(unit);
                var atPatrolPoint = position.z > 9.5f;
                var atMoveEnd = position.z < 0.5f && position.x > 9.5f;
                if (goingOut ? atPatrolPoint : atMoveEnd)
                {
                    legs++;
                    goingOut = !goingOut;
                }
            }

            Assert.GreaterOrEqual(legs, 3, "out, back to the move's end and out again");
            Assert.AreEqual(OrderType.Patrol, _world.Get<ActiveOrder>(unit).Value.Type, "still patrolling");
        }

        [Test]
        public void Patrol_EngagesEnemiesOnTheWay()
        {
            var unit = _world.Arm(_world.SpawnUnit(1, float3.zero), range: 3f);
            var enemy = _world.SpawnUnit(2, new float3(10f, 0f, 3f));
            Patrol(unit, new float3(20f, 0f, 0f));

            _world.Run(6f);

            Assert.IsFalse(_world.EntityManager.Exists(enemy), "killed on the way");
            Assert.AreEqual(OrderType.Patrol, _world.Get<ActiveOrder>(unit).Value.Type, "and kept patrolling");
        }

        [Test]
        public void Escort_FollowsItsWard_DefendsIt_AndEndsWhenItDies()
        {
            var ward = _world.SpawnUnit(1, float3.zero);
            var escort = _world.Arm(_world.SpawnUnit(1, new float3(-3f, 0f, 0f)), range: 3f);
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.Escort, Unit = escort, Target = ward, Position = float3.zero,
            });
            _world.Tick();
            Assert.AreEqual(OrderType.Escort, _world.Get<ActiveOrder>(escort).Value.Type);

            _world.MoveTo(ward, new float3(30f, 0f, 0f));
            _world.Run(8f);
            var gap = math.distance(_world.PositionOf(escort).xz, _world.PositionOf(ward).xz);
            Assert.LessOrEqual(gap, EscortSystem.FollowDistance + 1.5f, "kept up with the ward");

            var raider = _world.SpawnUnit(2, new float3(30f, 0f, 6f));
            _world.Run(4f);
            Assert.IsFalse(_world.EntityManager.Exists(raider), "fought off the raider");
            Assert.AreEqual(OrderType.Escort, _world.Get<ActiveOrder>(escort).Value.Type);

            _world.EntityManager.DestroyEntity(ward);
            _world.Tick(frames: 2);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(escort), "the escort ends with its ward");
        }

        private void Patrol(Entity unit, float3 point) => _world.Command(1, new PlayerCommand
        {
            Type = CommandType.Patrol, Unit = unit, Position = point,
        });
    }
}
