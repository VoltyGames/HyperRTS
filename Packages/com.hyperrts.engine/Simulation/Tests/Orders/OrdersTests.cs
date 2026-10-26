using System;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Orders
{
    /// <summary>Order lifecycle: move, queue, stop, owner change, patrol loops and escort.</summary>
    public class OrdersTests
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

        private Entity SpawnSelected(float3 position)
        {
            var unit = _world.SpawnUnit(1, position);
            _world.EntityManager.SetComponentEnabled<Selected>(unit, true);
            return unit;
        }

        private void Command(CommandType type, float3 position, bool queue = false) =>
            _world.Command(1, new PlayerCommand { Type = type, Position = position, Queue = queue });

        private void Patrol(Entity unit, float3 point, bool queue = false) => _world.Command(1, new PlayerCommand
        {
            Type = CommandType.Patrol, Unit = unit, Position = point, Queue = queue,
        });

        /// <summary>Runs for the given time and counts the legs between the two ends of a patrol.</summary>
        private int CountLegs(Entity unit, float seconds, Func<float3, bool> atFar, Func<float3, bool> atNear)
        {
            var legs = 0;
            var goingOut = true;
            for (var t = 0f; t < seconds; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var position = _world.GetPosition(unit);
                if (goingOut ? atFar(position) : atNear(position))
                {
                    legs++;
                    goingOut = !goingOut;
                }
            }

            return legs;
        }

        [Test]
        public void RightClickMove_ReachesThePointAndGoesIdle()
        {
            var unit = SpawnSelected(new float3(0f, 2f, 0f));
            var goal = new float3(10f, 0f, 5f);

            Command(CommandType.Smart, goal);
            _world.Run(4f);

            Assert.Less(math.distance(_world.GetPosition(unit).xz, goal.xz), 0.2f);
            Assert.AreEqual(0f, _world.GetPosition(unit).y, "movement settles the unit on the flat ground");
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(unit));
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(unit));
        }

        [Test]
        public void ShiftQueuedOrders_RunInOrder()
        {
            var unit = SpawnSelected(float3.zero);
            var first = new float3(6f, 0f, 0f);
            var second = new float3(6f, 0f, 6f);

            Command(CommandType.Move, first);
            Command(CommandType.Move, second, queue: true);
            _world.Tick();
            Assert.AreEqual(1, _world.EntityManager.GetBuffer<QueuedOrder>(unit).Length);

            var reachedFirst = false;
            for (var t = 0f; t < 6f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var position = _world.GetPosition(unit);
                reachedFirst |= math.distance(position.xz, first.xz) < 0.3f;
                Assert.IsTrue(reachedFirst || math.distance(position.xz, second.xz) > 1f, "second goal reached first");
            }

            Assert.IsTrue(reachedFirst);
            Assert.Less(math.distance(_world.GetPosition(unit).xz, second.xz), 0.2f);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(unit));
        }

        [Test]
        public void QueuedOrder_DropsATargetPickedUpWhileIdle()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            _world.AddWeapon(unit);
            var enemy = _world.SpawnUnit(2, new float3(3f, 0f, 0f));
            var goal = new float3(-10f, 0f, 0f);
            _world.Issue(unit, new Order { Type = OrderType.Move, Position = new float3(5f, 0f, 0f) });
            _world.Issue(unit, new Order { Type = OrderType.Move, Position = goal }, true);
            // The first move finishes, leaving the queued one, and the idle unit picks up a target.
            _world.EntityManager.SetComponentEnabled<ActiveOrder>(unit, false);
            _world.EntityManager.SetComponentData(unit, new AttackTarget { Value = enemy });
            _world.EntityManager.SetComponentEnabled<AttackTarget>(unit, true);

            _world.World.GetExistingSystem<OrderDispatchSystem>().Update(_world.World.Unmanaged);

            Assert.IsTrue(_world.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(goal, _world.Get<ActiveOrder>(unit).Value.Position);
            Assert.IsFalse(_world.IsEnabled<AttackTarget>(unit), "the queued move isn't held back by the old target");
        }

        [Test]
        public void Stop_HaltsAndClearsOrders()
        {
            var unit = SpawnSelected(float3.zero);
            Command(CommandType.Move, new float3(30f, 0f, 0f));
            Command(CommandType.Move, new float3(30f, 0f, 30f), queue: true);
            _world.Run(0.5f);

            Command(CommandType.Stop, float3.zero);
            _world.Tick();
            var stopped = _world.GetPosition(unit);
            _world.Run(1f);

            Assert.Less(math.distance(_world.GetPosition(unit), stopped), 0.01f);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<QueuedOrder>(unit).Length);
        }

        [Test]
        public void OwnerChange_StopsTheOldOwnersOrders()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            var goal = new Order { Type = OrderType.Move, Position = new float3(20f, 0f, 0f) };
            _world.Issue(unit, goal);
            _world.Issue(unit, goal, true);
            _world.EntityManager.SetComponentEnabled<MoveDestination>(unit, true);
            var events = _world.EntityManager.CreateEntityQuery(typeof(GameEvents)).GetSingletonEntity();
            _world.EntityManager.GetBuffer<OwnerChanged>(events)
                .Add(new OwnerChanged { Entity = unit, From = 1, To = 2 });

            _world.World.GetExistingSystem<OwnerChangeSystem>().Update(_world.World.Unmanaged);

            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<QueuedOrder>(unit).Length);
            Assert.IsFalse(_world.IsEnabled<MoveDestination>(unit));
        }

        [Test]
        public void Patrol_LoopsBetweenStartAndTarget()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            Patrol(unit, new float3(10f, 0f, 0f));

            var legs = CountLegs(unit, 10f, position => position.x > 9.5f, position => position.x < 0.5f);

            Assert.GreaterOrEqual(legs, 3, "out, back and out again");
            Assert.IsTrue(_world.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(OrderType.Patrol, _world.Get<ActiveOrder>(unit).Value.Type);
        }

        [Test]
        public void QueuedPatrol_LoopsBetweenTheEndOfTheQueueAndTheNewPoint()
        {
            var unit = _world.SpawnUnit(1, float3.zero);
            _world.MoveTo(unit, new float3(10f, 0f, 0f));
            Patrol(unit, new float3(10f, 0f, 10f), queue: true);

            var legs = CountLegs(unit, 14f, position => position.z > 9.5f,
                position => position.z < 0.5f && position.x > 9.5f);

            Assert.GreaterOrEqual(legs, 3, "out, back to the move's end and out again");
            Assert.AreEqual(OrderType.Patrol, _world.Get<ActiveOrder>(unit).Value.Type, "still patrolling");
        }

        [Test]
        public void Patrol_EngagesEnemiesOnTheWay()
        {
            var unit = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 3f);
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
            var escort = _world.AddWeapon(_world.SpawnUnit(1, new float3(-3f, 0f, 0f)), range: 3f);
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.Escort, Unit = escort, Target = ward, Position = float3.zero,
            });
            _world.Tick();
            Assert.AreEqual(OrderType.Escort, _world.Get<ActiveOrder>(escort).Value.Type);

            _world.MoveTo(ward, new float3(30f, 0f, 0f));
            _world.Run(8f);
            var gap = math.distance(_world.GetPosition(escort).xz, _world.GetPosition(ward).xz);
            Assert.LessOrEqual(gap, EscortSystem.FollowDistance + 1.5f, "kept up with the ward");

            var raider = _world.SpawnUnit(2, new float3(30f, 0f, 6f));
            _world.Run(4f);
            Assert.IsFalse(_world.EntityManager.Exists(raider), "fought off the raider");
            Assert.AreEqual(OrderType.Escort, _world.Get<ActiveOrder>(escort).Value.Type);

            _world.EntityManager.DestroyEntity(ward);
            _world.Tick(frames: 2);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(escort), "the escort ends with its ward");
        }
    }
}
