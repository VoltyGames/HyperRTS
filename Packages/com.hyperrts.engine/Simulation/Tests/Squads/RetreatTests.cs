using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Squads
{
    /// <summary>
    /// A retreating squad runs to the nearest friendly retreat point faster, takes half damage, fires nothing, shrugs
    /// off suppression even when pinned, and goes back to normal on arrival.
    /// </summary>
    public class RetreatTests : SquadFixture
    {
        private Entity Squad(float3 position)
        {
            var squad = World.SpawnSquad(1, position);
            World.AddWeapon(squad, new TestWeapon { Range = 6f, Damage = 10f, Cooldown = 0.2f });
            return squad;
        }

        private Entity Point(byte owner, float3 position)
        {
            var point = World.SpawnBuilding(owner, position, new float2(4f, 4f));
            World.Add(point, new RetreatPoint { Radius = 8f });
            return point;
        }

        private void Retreat(Entity squad) =>
            World.Command(1, new PlayerCommand { Type = CommandType.Retreat, Unit = squad });

        private float X(Entity entity) => World.Get<LocalTransform>(entity).Position.x;

        [Test]
        public void Retreat_RunsFasterThanAMoveAndEndsOnArrival()
        {
            var squad = Squad(float3.zero);
            Point(1, new float3(60f, 0f, 0f));

            Retreat(squad);
            World.Run(2f);
            Assert.That(X(squad), Is.InRange(11f, 13f), $"1.25 times the speed of 5, at {X(squad)}");
            Assert.IsTrue(World.IsEnabled<ActiveOrder>(squad));

            Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<ActiveOrder>(squad), 20f), "the order ends");
            Assert.That(X(squad), Is.GreaterThan(60f - 8f - 1f), "inside the point's radius");
            World.Tick(frames: 2);
            Assert.AreEqual(1f, World.Get<Pace>(squad).Speed, 1e-3f, "back to normal pace");
        }

        [Test]
        public void Retreat_GoesToTheNearestFriendlyPoint()
        {
            var squad = Squad(float3.zero);
            Point(1, new float3(-80f, 0f, 0f));
            Point(2, new float3(20f, 0f, 0f));
            Point(1, new float3(50f, 0f, 40f));

            Retreat(squad);
            World.Tick(frames: 3);

            Assert.AreEqual(50f, World.Get<ActiveOrder>(squad).Value.Position.x, 1e-3f);
        }

        [Test]
        public void Retreating_IsOnWhileTheRetreatOrderRuns()
        {
            var squad = Squad(float3.zero);
            Point(1, new float3(60f, 0f, 0f));
            Retreat(squad);
            World.Tick(frames: 3);
            Assert.IsTrue(World.IsEnabled<Retreating>(squad));

            World.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = squad, Position = new float3(-20f, 0f, 0f) });
            World.Tick(frames: 3);

            Assert.IsFalse(World.IsEnabled<Retreating>(squad), "a new order ends the retreat");
        }

        [Test]
        public void Retreat_LeavesAPointThatGoesOffline()
        {
            var squad = Squad(float3.zero);
            var near = Point(1, new float3(60f, 0f, 0f));
            Point(1, new float3(-90f, 0f, 0f));
            Retreat(squad);
            World.Tick(frames: 3);
            Assert.AreEqual(60f, World.Get<ActiveOrder>(squad).Value.Position.x, 1e-3f);

            World.SetOffline(near, true);
            World.Tick(frames: 3);

            Assert.AreEqual(-90f, World.Get<ActiveOrder>(squad).Value.Position.x, 1e-3f);
        }

        [Test]
        public void Retreat_WithNoPointDoesNothing()
        {
            var squad = Squad(float3.zero);
            Point(2, new float3(20f, 0f, 0f));

            Retreat(squad);
            World.Run(1f);

            Assert.IsFalse(World.IsEnabled<ActiveOrder>(squad));
        }

        [Test]
        public void Retreating_TakesHalfDamageAndFiresNothing()
        {
            var squad = Squad(float3.zero);
            var enemy = World.SpawnUnit(2, new float3(3f, 0f, 0f), new TestUnit { MaxHealth = 1000f });
            Point(1, new float3(-80f, 0f, 0f));
            World.Run(1f);
            Assert.Less(World.Get<Health>(enemy).Current, 1000f, "the squad fights before it retreats");

            Retreat(squad);
            World.Run(0.1f);
            var before = World.Get<Health>(enemy).Current;
            var squadBefore = World.Get<Health>(squad).Current;
            World.QueueDamage(DamageEvent.Direct(squad, float3.zero, enemy, 2, 20f));
            World.Run(0.3f);

            Assert.AreEqual(before, World.Get<Health>(enemy).Current, "no shots while retreating");
            Assert.AreEqual(squadBefore - 10f, World.Get<Health>(squad).Current, 1e-3f);
        }

        [Test]
        public void Pinned_CanStillRetreatAndIsFreedByIt()
        {
            var squad = Squad(float3.zero);
            Point(1, new float3(40f, 0f, 0f));
            World.Set(squad, new Suppression { Value = 1f });
            // Pace follows posture a frame later, after cover is known.
            World.Tick();
            World.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = squad, Position = new float3(0f, 0f, 30f) });
            World.Run(0.5f);
            Assert.AreEqual(0f, World.Get<LocalTransform>(squad).Position.z, 1e-3f, $"pinned, at {World.Get<LocalTransform>(squad).Position.z} with suppression {World.Get<Suppression>(squad).Value}");

            Retreat(squad);
            World.Run(1f);
            Assert.Greater(X(squad), 3f, "it runs");
            Assert.AreEqual(0f, World.Get<Suppression>(squad).Value, 1e-3f, "suppression is cleared");

            World.QueueDamage(new DamageEvent
            {
                Target = squad,
                Origin = new float3(0f, 0f, 20f),
                SourceOwner = 2,
                Profile = new DamageProfile { EdgeFactor = 1f, Suppression = 1f },
            });
            World.Tick(frames: 2);
            Assert.AreEqual(0f, World.Get<Suppression>(squad).Value, 1e-3f, "and fire doesn't suppress it");
        }
    }
}
