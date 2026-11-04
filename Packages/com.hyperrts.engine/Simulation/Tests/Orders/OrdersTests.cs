using HyperRTS.Simulation.Common.Events;
using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Orders
{
    /// <summary>Order lifecycle: move, queue, stop, owner change, patrol loops and escort.</summary>
    public class OrdersTests : SimulationFixture
    {
        private Entity SpawnSelected(float3 position)
        {
            var unit = World.SpawnUnit(1, position);
            World.SetEnabled<Selected>(unit, true);
            return unit;
        }

        private void Command(CommandType type, float3 position, bool queue = false) =>
            World.Command(1, new PlayerCommand { Type = type, Position = position, Queue = queue });

        private void Patrol(Entity unit, float3 point, bool queue = false) => World.Command(1, new PlayerCommand
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
                World.Tick();
                var position = World.GetPosition(unit);
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
            World.Run(4f);

            Assert.Less(math.distance(World.GetPosition(unit).xz, goal.xz), 0.2f);
            Assert.AreEqual(0f, World.GetPosition(unit).y, "movement settles the unit on the flat ground");
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(unit));
            Assert.IsFalse(World.IsEnabled<MoveDestination>(unit));
        }

        [Test]
        public void ShiftQueuedOrders_RunInOrder()
        {
            var unit = SpawnSelected(float3.zero);
            var first = new float3(6f, 0f, 0f);
            var second = new float3(6f, 0f, 6f);

            Command(CommandType.Move, first);
            Command(CommandType.Move, second, queue: true);
            World.Tick();
            Assert.AreEqual(1, World.Buffer<QueuedOrder>(unit).Length);

            var reachedFirst = false;
            for (var t = 0f; t < 6f; t += TestWorld.FrameTime)
            {
                World.Tick();
                var position = World.GetPosition(unit);
                reachedFirst |= math.distance(position.xz, first.xz) < 0.3f;
                Assert.IsTrue(reachedFirst || math.distance(position.xz, second.xz) > 1f, "second goal reached first");
            }

            Assert.IsTrue(reachedFirst);
            Assert.Less(math.distance(World.GetPosition(unit).xz, second.xz), 0.2f);
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(unit));
        }

        [Test]
        public void QueuedOrder_DropsATargetPickedUpWhileIdle()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            World.AddWeapon(unit);
            var enemy = World.SpawnUnit(2, new float3(3f, 0f, 0f));
            var goal = new float3(-10f, 0f, 0f);
            World.Issue(unit, new Order { Type = OrderType.Move, Position = new float3(5f, 0f, 0f) });
            World.Issue(unit, new Order { Type = OrderType.Move, Position = goal }, true);
            // The first move finishes, leaving the queued one, and the idle unit picks up a target.
            World.SetEnabled<ActiveOrder>(unit, false);
            World.Set(unit, new AttackTarget { Value = enemy });
            World.SetEnabled<AttackTarget>(unit, true);

            World.World.GetExistingSystem<OrderDispatchSystem>().Update(World.World.Unmanaged);

            Assert.IsTrue(World.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(goal, World.Get<ActiveOrder>(unit).Value.Position);
            Assert.IsFalse(World.IsEnabled<AttackTarget>(unit), "the queued move isn't held back by the old target");
        }

        [Test]
        public void Stop_HaltsAndClearsOrders()
        {
            var unit = SpawnSelected(float3.zero);
            Command(CommandType.Move, new float3(30f, 0f, 0f));
            Command(CommandType.Move, new float3(30f, 0f, 30f), queue: true);
            World.Run(0.5f);

            Command(CommandType.Stop, float3.zero);
            World.Tick();
            var stopped = World.GetPosition(unit);
            World.Run(1f);

            Assert.Less(math.distance(World.GetPosition(unit), stopped), 0.01f);
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(0, World.Buffer<QueuedOrder>(unit).Length);
        }

        [Test]
        public void OwnerChange_StopsTheOldOwnersOrders()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            var goal = new Order { Type = OrderType.Move, Position = new float3(20f, 0f, 0f) };
            World.Issue(unit, goal);
            World.Issue(unit, goal, true);
            World.SetEnabled<MoveDestination>(unit, true);
            var events = World.Query(typeof(GameEvents)).GetSingletonEntity();
            World.Buffer<OwnerChanged>(events)
                .Add(new OwnerChanged { Entity = unit, From = 1, To = 2 });

            World.World.GetExistingSystem<OwnerChangeSystem>().Update(World.World.Unmanaged);

            Assert.IsFalse(World.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(0, World.Buffer<QueuedOrder>(unit).Length);
            Assert.IsFalse(World.IsEnabled<MoveDestination>(unit));
        }

        [Test]
        public void Patrol_LoopsBetweenStartAndTarget()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            Patrol(unit, new float3(10f, 0f, 0f));

            var legs = CountLegs(unit, 10f, position => position.x > 9.5f, position => position.x < 0.5f);

            Assert.GreaterOrEqual(legs, 3, "out, back and out again");
            Assert.IsTrue(World.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(OrderType.Patrol, World.Get<ActiveOrder>(unit).Value.Type);
        }

        [Test]
        public void QueuedPatrol_LoopsBetweenTheEndOfTheQueueAndTheNewPoint()
        {
            var unit = World.SpawnUnit(1, float3.zero);
            World.MoveTo(unit, new float3(10f, 0f, 0f));
            Patrol(unit, new float3(10f, 0f, 10f), queue: true);

            var legs = CountLegs(unit, 14f, position => position.z > 9.5f,
                position => position.z < 0.5f && position.x > 9.5f);

            Assert.GreaterOrEqual(legs, 3, "out, back to the move's end and out again");
            Assert.AreEqual(OrderType.Patrol, World.Get<ActiveOrder>(unit).Value.Type, "still patrolling");
        }

        [Test]
        public void Patrol_EngagesEnemiesOnTheWay()
        {
            var unit = World.AddWeapon(World.SpawnUnit(1, float3.zero), range: 3f);
            var enemy = World.SpawnUnit(2, new float3(10f, 0f, 3f));
            Patrol(unit, new float3(20f, 0f, 0f));

            World.Run(6f);

            Assert.IsFalse(World.EntityManager.Exists(enemy), "killed on the way");
            Assert.AreEqual(OrderType.Patrol, World.Get<ActiveOrder>(unit).Value.Type, "and kept patrolling");
        }

        [Test]
        public void Escort_FollowsItsWard_DefendsIt_AndEndsWhenItDies()
        {
            var ward = World.SpawnUnit(1, float3.zero);
            var escort = World.AddWeapon(World.SpawnUnit(1, new float3(-3f, 0f, 0f)), range: 3f);
            World.Command(1, new PlayerCommand
            {
                Type = CommandType.Escort, Unit = escort, Target = ward, Position = float3.zero,
            });
            World.Tick();
            Assert.AreEqual(OrderType.Escort, World.Get<ActiveOrder>(escort).Value.Type);

            World.MoveTo(ward, new float3(30f, 0f, 0f));
            World.Run(8f);
            var gap = math.distance(World.GetPosition(escort).xz, World.GetPosition(ward).xz);
            Assert.LessOrEqual(gap, EscortSystem.FollowDistance + 1.5f, "kept up with the ward");

            var raider = World.SpawnUnit(2, new float3(30f, 0f, 6f));
            World.Run(4f);
            Assert.IsFalse(World.EntityManager.Exists(raider), "fought off the raider");
            Assert.AreEqual(OrderType.Escort, World.Get<ActiveOrder>(escort).Value.Type);

            World.EntityManager.DestroyEntity(ward);
            World.Tick(frames: 2);
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(escort), "the escort ends with its ward");
        }
    }
}
