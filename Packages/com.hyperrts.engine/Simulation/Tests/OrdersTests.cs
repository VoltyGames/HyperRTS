using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Player commands → orders: move, queue, stop and right-click resolution.</summary>
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

        private void Command(CommandType type, float3 position, Entity target = default, bool queue = false) =>
            _world.Command(1, new PlayerCommand { Type = type, Position = position, Target = target, Queue = queue });

        private float3 PositionOf(Entity entity) => _world.Get<LocalTransform>(entity).Position;

        /// <summary>Runs only the command system, so other behaviours can't act on the new order yet.</summary>
        private void RunCommands() =>
            _world.World.GetExistingSystem<UnitCommandSystem>().Update(_world.World.Unmanaged);

        [Test]
        public void RightClickMove_ReachesThePointAndGoesIdle()
        {
            var unit = SpawnSelected(new float3(0f, 2f, 0f));
            var goal = new float3(10f, 0f, 5f);

            Command(CommandType.Smart, goal);
            _world.Run(4f);

            Assert.Less(math.distance(PositionOf(unit).xz, goal.xz), 0.2f);
            Assert.AreEqual(0f, PositionOf(unit).y, "movement settles the unit on the flat ground");
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
                var position = PositionOf(unit);
                reachedFirst |= math.distance(position.xz, first.xz) < 0.3f;
                Assert.IsTrue(reachedFirst || math.distance(position.xz, second.xz) > 1f, "second goal reached first");
            }

            Assert.IsTrue(reachedFirst);
            Assert.Less(math.distance(PositionOf(unit).xz, second.xz), 0.2f);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(unit));
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
            var stopped = PositionOf(unit);
            _world.Run(1f);

            Assert.Less(math.distance(PositionOf(unit), stopped), 0.01f);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(unit));
            Assert.AreEqual(0, _world.EntityManager.GetBuffer<QueuedOrder>(unit).Length);
        }

        [Test]
        public void Smart_OnHostileWithWeapon_IssuesAttack()
        {
            var armed = SpawnSelected(float3.zero);
            var unarmed = SpawnSelected(new float3(2f, 0f, 0f));
            _world.EntityManager.AddComponent<Weapon>(armed);
            var enemy = _world.SpawnUnit(2, new float3(8f, 0f, 0f));

            Command(CommandType.Smart, new float3(8f, 0f, 0f), enemy);
            RunCommands();

            var order = _world.Get<ActiveOrder>(armed).Value;
            Assert.AreEqual(OrderType.Attack, order.Type);
            Assert.AreEqual(enemy, order.Target);
            Assert.AreEqual(OrderType.Move, _world.Get<ActiveOrder>(unarmed).Value.Type, "no weapon: just move");
        }

        [Test]
        public void AttackMove_OnHostile_IssuesAttack_ElseAttackMovesToThePoint()
        {
            var goal = new float3(8f, 0f, 0f);
            var enemy = _world.SpawnUnit(2, goal);
            var friend = _world.SpawnUnit(1, goal);
            var onEnemy = _world.SpawnUnit(1, float3.zero);
            var onFriend = _world.SpawnUnit(1, new float3(0f, 0f, 2f));
            var onGround = _world.SpawnUnit(1, new float3(0f, 0f, -2f));
            foreach (var (unit, target) in new[] { (onEnemy, enemy), (onFriend, friend), (onGround, Entity.Null) })
            {
                _world.EntityManager.AddComponent<Weapon>(unit);
                _world.Command(1, new PlayerCommand
                {
                    Type = CommandType.AttackMove, Unit = unit, Target = target, Position = goal,
                });
            }

            RunCommands();

            var attack = _world.Get<ActiveOrder>(onEnemy).Value;
            Assert.AreEqual(OrderType.Attack, attack.Type);
            Assert.AreEqual(enemy, attack.Target);
            foreach (var unit in new[] { onFriend, onGround })
            {
                var order = _world.Get<ActiveOrder>(unit).Value;
                Assert.AreEqual(OrderType.AttackMove, order.Type, "not hostile: attack-move");
                Assert.Less(math.distance(order.Position, goal), 1e-3f);
            }
        }

        [Test]
        public void Smart_OnResourceNodeWithHarvester_IssuesGather()
        {
            var harvester = SpawnSelected(float3.zero);
            _world.EntityManager.AddComponent<Harvester>(harvester);
            var node = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(node, LocalTransform.FromPosition(new float3(6f, 0f, 0f)));
            _world.EntityManager.AddComponentData(node, new Faction { Value = Faction.Neutral });
            _world.EntityManager.AddComponent<ResourceNode>(node);

            Command(CommandType.Smart, new float3(6f, 0f, 0f), node);
            RunCommands();

            var order = _world.Get<ActiveOrder>(harvester).Value;
            Assert.AreEqual(OrderType.Gather, order.Type);
            Assert.AreEqual(node, order.Target);
        }

        [Test]
        public void Smart_OnGround_IssuesMove_OnlyToOwnSelectedUnits()
        {
            var own = SpawnSelected(float3.zero);
            var enemy = _world.SpawnUnit(2, new float3(3f, 0f, 0f));
            _world.EntityManager.SetComponentEnabled<Selected>(enemy, true);

            Command(CommandType.Smart, new float3(5f, 0f, 5f));
            RunCommands();

            Assert.IsTrue(_world.IsEnabled<ActiveOrder>(own));
            Assert.AreEqual(OrderType.Move, _world.Get<ActiveOrder>(own).Value.Type);
            Assert.IsFalse(_world.IsEnabled<ActiveOrder>(enemy), "another player's selection is not commanded");
        }
    }
}
