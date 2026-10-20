using HyperRTS.Simulation.Commands;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Order behaviour as data: engine traits, game orders with their own traits, command type sets.</summary>
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

        [Test]
        public void EngineOrders_TakeTheEngineTraits_WhateverTheIssuerSet()
        {
            var unit = SpawnSelected(float3.zero);
            _world.EntityManager.AddBuffer<SmartOrderRule>(unit)
                .Add(SmartOrderRule.For<UnitTag>(OrderType.Capture, OrderTraits.EngagesWhileMoving, RelationMask.Enemy));
            var enemy = _world.SpawnUnit(2, new float3(10f, 0f, 0f));

            SmartOn(enemy);
            RunCommands();
            Assert.AreEqual(OrderTraits.None, _world.Get<ActiveOrder>(unit).Value.Traits, "capture has no traits");

            _world.Command(1, new PlayerCommand { Type = CommandType.Move, Position = new float3(-10f, 0f, 0f) });
            RunCommands();
            Assert.AreEqual(OrderTraits.Formation, _world.Get<ActiveOrder>(unit).Value.Traits);
        }

        [Test]
        public void GameOrder_KeepsItsTraits_AndAFormationOneWalksLikeAMove()
        {
            var unit = SpawnSelected(float3.zero);
            _world.EntityManager.AddBuffer<SmartOrderRule>(unit)
                .Add(SmartOrderRule.For<UnitTag>(GameOrder, OrderTraits.Formation, RelationMask.Enemy));
            var enemy = _world.SpawnUnit(2, new float3(10f, 0f, 0f));

            SmartOn(enemy);
            RunCommands();
            var order = _world.Get<ActiveOrder>(unit).Value;
            Assert.AreEqual(GameOrder, order.Type);
            Assert.AreEqual(OrderTraits.Formation, order.Traits);

            _world.Run(1f);
            Assert.Greater(_world.GetPosition(unit).x, 2f, "the move order runs a formation trait");
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
