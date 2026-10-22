using System;
using HyperRTS.Simulation.Commands;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Order behaviour as data: traits by order type, registered game orders, command type sets.</summary>
    public class OrderTraitsTests
    {
        private const OrderType GameOrder = OrderType.GameFirst + 2;

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

        private void RunCommands() =>
            _world.World.GetExistingSystem<UnitCommandSystem>().Update(_world.World.Unmanaged);

        private void SmartOn(Entity target) => _world.Command(1, new PlayerCommand
        {
            Type = CommandType.Smart, Position = _world.GetPosition(target), Target = target,
        });

        private void Register(OrderType type, OrderTraits traits)
        {
            using var query = _world.EntityManager.CreateEntityQuery(typeof(OrderTraitTable));
            query.GetSingletonRW<OrderTraitTable>().ValueRW.Register(type, traits);
        }

        [Test]
        public void EngineOrders_TakeTheirTraitsFromTheType()
        {
            var table = _world.Singleton<OrderTraitTable>();
            var walks = OrderTraits.Formation | OrderTraits.MovesToPosition;

            Assert.AreEqual(walks, table.Traits(OrderType.Move));
            Assert.IsTrue(table.Has(OrderType.Patrol, OrderTraits.Loops), "patrol legs loop");
            Assert.IsFalse(table.Has(OrderType.AttackMove, OrderTraits.Loops));
            Assert.AreEqual(OrderTraits.EngagesWhileMoving, table.Traits(OrderType.Escort));
            Assert.AreEqual(OrderTraits.None, table.Traits(OrderType.Capture));
        }

        [Test]
        public void GameOrder_TakesItsRegisteredTraits_AndAMovingOneWalksLikeAMove()
        {
            Register(GameOrder, OrderTraits.Formation | OrderTraits.MovesToPosition);
            var unit = SpawnSelected(float3.zero);
            _world.EntityManager.AddBuffer<SmartOrderRule>(unit)
                .Add(SmartOrderRule.For<UnitTag>(GameOrder, RelationMask.Enemy));
            var enemy = _world.SpawnUnit(2, new float3(10f, 0f, 0f));

            SmartOn(enemy);
            RunCommands();
            Assert.AreEqual(GameOrder, _world.Get<ActiveOrder>(unit).Value.Type);

            _world.Run(1f);
            Assert.Greater(_world.GetPosition(unit).x, 2f, "the move order runs a game order that moves to its position");
        }

        [Test]
        public void GameOrder_WithoutRegisteredTraits_FailsToIssue()
        {
            var unit = _world.SpawnUnit(1, float3.zero);

            Assert.Throws<InvalidOperationException>(() => _world.Issue(unit, new Order { Type = GameOrder }));
        }

        [Test]
        public void EngineOrder_CannotBeRegistered()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Register(OrderType.Capture, OrderTraits.Formation));
        }

        [Test]
        public void TypeSet_HoldsItsRangeAndAddedTypes()
        {
            var set = TypeSet.Range(CommandType.UseAbility, CommandType.UsePower).With(CommandType.Surrender);

            Assert.IsTrue(set.Contains(CommandType.UseAbility));
            Assert.IsTrue(set.Contains(CommandType.UsePower));
            Assert.IsTrue(set.Contains(CommandType.Surrender));
            Assert.IsFalse(set.Contains(CommandType.Patrol));
        }

        [Test]
        public void TypeSet_KeepsEveryByteValueApart()
        {
            var set = TypeSet.Single((CommandType)64).With(CommandType.GameFirst + 3).With((CommandType)255)
                .With(CommandType.Smart);

            Assert.IsTrue(set.Contains((CommandType)64));
            Assert.IsTrue(set.Contains(CommandType.GameFirst + 3));
            Assert.IsTrue(set.Contains((CommandType)255));
            Assert.IsTrue(set.Contains(CommandType.Smart));
            Assert.IsFalse(set.Contains(CommandType.None), "64 doesn't wrap onto 0");
            Assert.IsFalse(set.Contains(CommandType.AttackMove), "GameFirst + 3 doesn't wrap onto 3");
            Assert.IsFalse(set.Contains((CommandType)192));

            var top = TypeSet.Range((CommandType)250, (CommandType)255);
            Assert.IsTrue(top.Contains((CommandType)255), "a range ending at 255 ends");
            Assert.IsFalse(top.Contains((CommandType)249));
        }
    }
}
