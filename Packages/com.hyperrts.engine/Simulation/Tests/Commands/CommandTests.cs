using HyperRTS.Simulation.Combat.Weapons;
using System;
using HyperRTS.Simulation.Commands;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources.Gathering;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Commands
{
    /// <summary>Which order a command gives: right-click and attack-move resolution, order traits and type sets.</summary>
    public class CommandTests : SimulationFixture
    {
        private const OrderType GameOrder = OrderType.GameFirst + 2;

        private Entity SpawnSelected(float3 position)
        {
            var unit = World.SpawnUnit(1, position);
            World.SetEnabled<Selected>(unit, true);
            return unit;
        }

        private void Command(CommandType type, float3 position, Entity target = default) =>
            World.Command(1, new PlayerCommand { Type = type, Position = position, Target = target });

        private void SmartOn(Entity target) => Command(CommandType.Smart, World.GetPosition(target), target);

        /// <summary>Runs only the command system, so other behaviours can't act on the new order yet.</summary>
        private void RunCommands() =>
            World.World.GetExistingSystem<UnitCommandSystem>().Update(World.World.Unmanaged);

        private void Register(OrderType type, OrderTraits traits)
        {
            using var query = World.Query(typeof(OrderTraitTable));
            query.GetSingletonRW<OrderTraitTable>().ValueRW.Register(type, traits);
        }

        [Test]
        public void Smart_OnHostileWithWeapon_IssuesAttack()
        {
            var armed = SpawnSelected(float3.zero);
            var unarmed = SpawnSelected(new float3(2f, 0f, 0f));
            World.EntityManager.AddBuffer<WeaponMount>(armed).Add(new WeaponMount { Weapon = new Weapon { Targets = WeaponTargets.Surface } });
            var enemy = World.SpawnUnit(2, new float3(8f, 0f, 0f));

            SmartOn(enemy);
            RunCommands();

            var order = World.Get<ActiveOrder>(armed).Value;
            Assert.AreEqual(OrderType.Attack, order.Type);
            Assert.AreEqual(enemy, order.Target);
            Assert.AreEqual(OrderType.Move, World.Get<ActiveOrder>(unarmed).Value.Type, "no weapon: just move");
        }

        [Test]
        public void AttackMove_OnHostile_IssuesAttack_ElseAttackMovesToThePoint()
        {
            var goal = new float3(8f, 0f, 0f);
            var enemy = World.SpawnUnit(2, goal);
            var friend = World.SpawnUnit(1, goal);
            var onEnemy = World.SpawnUnit(1, float3.zero);
            var onFriend = World.SpawnUnit(1, new float3(0f, 0f, 2f));
            var onGround = World.SpawnUnit(1, new float3(0f, 0f, -2f));
            foreach (var (unit, target) in new[] { (onEnemy, enemy), (onFriend, friend), (onGround, Entity.Null) })
            {
                World.EntityManager.AddBuffer<WeaponMount>(unit).Add(new WeaponMount { Weapon = new Weapon { Targets = WeaponTargets.Surface } });
                World.Command(1, new PlayerCommand
                {
                    Type = CommandType.AttackMove, Unit = unit, Target = target, Position = goal,
                });
            }

            RunCommands();

            var attack = World.Get<ActiveOrder>(onEnemy).Value;
            Assert.AreEqual(OrderType.Attack, attack.Type);
            Assert.AreEqual(enemy, attack.Target);
            foreach (var unit in new[] { onFriend, onGround })
            {
                var order = World.Get<ActiveOrder>(unit).Value;
                Assert.AreEqual(OrderType.AttackMove, order.Type, "not hostile: attack-move");
                Assert.Less(math.distance(order.Position, goal), 1e-3f);
            }
        }

        [Test]
        public void Smart_OnResourceNodeWithHarvester_IssuesGather()
        {
            var harvester = SpawnSelected(float3.zero);
            World.EntityManager.AddComponent<Harvester>(harvester);
            var node = World.EntityManager.CreateEntity();
            World.Add(node, LocalTransform.FromPosition(new float3(6f, 0f, 0f)));
            World.Add(node, new Owner { Value = Owner.Neutral });
            World.EntityManager.AddComponent<ResourceNode>(node);

            SmartOn(node);
            RunCommands();

            var order = World.Get<ActiveOrder>(harvester).Value;
            Assert.AreEqual(OrderType.Gather, order.Type);
            Assert.AreEqual(node, order.Target);
        }

        [Test]
        public void Smart_OnGround_IssuesMove_OnlyToOwnSelectedUnits()
        {
            var own = SpawnSelected(float3.zero);
            var enemy = World.SpawnUnit(2, new float3(3f, 0f, 0f));
            World.SetEnabled<Selected>(enemy, true);

            Command(CommandType.Smart, new float3(5f, 0f, 5f));
            RunCommands();

            Assert.IsTrue(World.IsEnabled<ActiveOrder>(own));
            Assert.AreEqual(OrderType.Move, World.Get<ActiveOrder>(own).Value.Type);
            Assert.IsFalse(World.IsEnabled<ActiveOrder>(enemy), "another player's selection is not commanded");
        }

        [Test]
        public void DefeatedPlayer_UnitCommandsAreIgnored()
        {
            var unit = SpawnSelected(float3.zero);
            World.SetEnabled<Defeated>(World.Player(1), true);

            Command(CommandType.Move, new float3(10f, 0f, 0f));
            RunCommands();

            Assert.IsFalse(World.IsEnabled<ActiveOrder>(unit), "a surrendered player's units take no orders");
        }

        [Test]
        public void SmartOrderRule_ChoosesItsOrder_ForMatchingTargets()
        {
            var unit = SpawnSelected(float3.zero);
            World.EntityManager.AddBuffer<SmartOrderRule>(unit)
                .Add(SmartOrderRule.For<UnitTag>(OrderType.Capture, RelationMask.Enemy));
            var enemy = World.SpawnUnit(2, new float3(10f, 0f, 0f));
            var friend = World.SpawnUnit(1, new float3(-10f, 0f, 0f));

            SmartOn(enemy);
            RunCommands();
            Assert.AreEqual(OrderType.Capture, World.Get<ActiveOrder>(unit).Value.Type);

            SmartOn(friend);
            RunCommands();
            Assert.AreEqual(OrderType.Move, World.Get<ActiveOrder>(unit).Value.Type, "the rule is for enemies only");
        }

        [Test]
        public void EngineOrders_TakeTheirTraitsFromTheType()
        {
            var table = World.Singleton<OrderTraitTable>();

            Assert.AreEqual(OrderTraits.Formation | OrderTraits.MovesToPosition, table.Traits(OrderType.Move));
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
            World.EntityManager.AddBuffer<SmartOrderRule>(unit)
                .Add(SmartOrderRule.For<UnitTag>(GameOrder, RelationMask.Enemy));
            var enemy = World.SpawnUnit(2, new float3(10f, 0f, 0f));

            SmartOn(enemy);
            RunCommands();
            Assert.AreEqual(GameOrder, World.Get<ActiveOrder>(unit).Value.Type);

            World.Run(1f);
            Assert.Greater(World.GetPosition(unit).x, 2f, "the move order runs a game order that moves to its position");
        }

        [Test]
        public void UnregisteredGameOrder_FailsToIssue_AndEngineOrdersCannotBeRegistered()
        {
            var unit = World.SpawnUnit(1, float3.zero);

            Assert.Throws<InvalidOperationException>(() => World.Issue(unit, new Order { Type = GameOrder }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Register(OrderType.Capture, OrderTraits.Formation));
        }

        [Test]
        public void TypeSet_HoldsItsRangeAndAddedTypes_AndKeepsEveryByteValueApart()
        {
            var set = TypeSet.Range(CommandType.UseAbility, CommandType.UsePower).With(CommandType.Surrender)
                .With((CommandType)64).With(CommandType.GameFirst + 3).With((CommandType)255);

            Assert.IsTrue(set.Contains(CommandType.UseAbility));
            Assert.IsTrue(set.Contains(CommandType.UsePower));
            Assert.IsTrue(set.Contains(CommandType.Surrender));
            Assert.IsTrue(set.Contains((CommandType)64));
            Assert.IsTrue(set.Contains(CommandType.GameFirst + 3));
            Assert.IsTrue(set.Contains((CommandType)255));
            Assert.IsFalse(set.Contains(CommandType.Patrol));
            Assert.IsFalse(set.Contains(CommandType.None), "64 doesn't wrap onto 0");
            Assert.IsFalse(set.Contains(CommandType.AttackMove), "GameFirst + 3 doesn't wrap onto 3");
            Assert.IsFalse(set.Contains((CommandType)192));

            var top = TypeSet.Range((CommandType)250, (CommandType)255);
            Assert.IsTrue(top.Contains((CommandType)255), "a range ending at 255 ends");
            Assert.IsFalse(top.Contains((CommandType)249));
        }
    }
}
