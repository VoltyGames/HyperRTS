using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Squads
{
    /// <summary>
    /// Fire on a squad builds suppression that falls after a quiet spell. Suppressed squads move crouched and slower,
    /// pinned ones lie prone, fire little and hold still without losing their orders.
    /// </summary>
    public class SuppressionTests : SquadFixture
    {
        private static readonly float3 Shooter = new(0f, 0f, 20f);

        private Entity Squad(float3 position, int size = 2) => World.SpawnSquad(1, position, size);

        private float Suppression(Entity squad) => World.Get<Suppression>(squad).Value;

        private void Hold(Entity squad, float value) =>
            World.Set(squad, new Suppression { Value = value });

        private void MoveTo(Entity squad, float3 goal) =>
            World.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = squad, Position = goal });

        [Test]
        public void Suppression_RisesWithHitsAndFallsAfterTheDelay()
        {
            var squad = Squad(float3.zero);

            World.HitSquad(squad, new TestHit { Suppression = 0.3f, From = Shooter });
            World.Tick();
            World.HitSquad(squad, new TestHit { Suppression = 0.3f, From = Shooter });
            World.Tick();
            Assert.AreEqual(0.6f, Suppression(squad), 0.01f);

            World.Run(2f);
            Assert.AreEqual(0.6f, Suppression(squad), 0.01f, "holds while the quiet is shorter than the delay");

            World.Run(6f);
            Assert.AreEqual(0f, Suppression(squad), 1e-3f);
        }

        [Test]
        public void Splash_AddsAQuarterForEachOtherMemberReached()
        {
            var squad = Squad(float3.zero, 4);

            // The struck member takes the hit's 0.2, each of the other three a quarter of it.
            World.HitSquad(squad, new TestHit { Suppression = 0.2f, Radius = 10f, From = Shooter });
            World.Tick();

            Assert.AreEqual(0.35f, Suppression(squad), 0.02f);
        }

        [Test]
        public void Posture_FollowsSuppressionAndMotion()
        {
            var squad = Squad(float3.zero);
            Hold(squad, 0.5f);
            MoveTo(squad, new float3(60f, 0f, 0f));
            World.Run(0.5f);
            Assert.AreEqual(Posture.Crouch, World.Buffer<SquadMember>(squad)[0].Posture, "suppressed squads move crouched");

            Hold(squad, 0.9f);
            World.Run(0.3f);
            foreach (var member in World.Buffer<SquadMember>(squad))
            {
                Assert.AreEqual(Posture.Prone, member.Posture, "pinned members lie prone");
            }

            Hold(squad, 0f);
            World.Run(0.5f);
            Assert.AreEqual(Posture.Stand, World.Buffer<SquadMember>(squad)[0].Posture, "an unsuppressed squad on the move stands");
        }

        [Test]
        public void Suppressed_MovesSlowerThanSteady()
        {
            var steady = Squad(new float3(0f, 0f, -20f));
            var suppressed = Squad(new float3(0f, 0f, 20f));
            MoveTo(steady, new float3(80f, 0f, -20f));
            World.Tick();
            MoveTo(suppressed, new float3(80f, 0f, 20f));
            for (var i = 0; i < 120; i++)
            {
                Hold(suppressed, 0.5f);
                World.Tick();
            }

            var rules = World.Singleton<SquadRules>();
            var ratio = World.Get<LocalTransform>(suppressed).Position.x / World.Get<LocalTransform>(steady).Position.x;
            Assert.AreEqual(rules.PostureSpeed(Posture.Crouch) * rules.SuppressedSpeed, ratio, 0.05f);
        }

        [Test]
        public void Pinned_HoldsStillKeepsItsOrderAndMovesOnWhenFree()
        {
            var squad = Squad(float3.zero);
            Hold(squad, 0.95f);
            MoveTo(squad, new float3(40f, 0f, 0f));

            World.Run(2f);
            Assert.AreEqual(0f, World.Get<LocalTransform>(squad).Position.x, 1e-3f, "a pinned squad doesn't move");
            Assert.IsTrue(World.IsEnabled<ActiveOrder>(squad), "it keeps the order");

            World.Run(8f);
            Assert.Greater(World.Get<LocalTransform>(squad).Position.x, 5f, "it goes on once suppression has fallen");
        }
    }
}
