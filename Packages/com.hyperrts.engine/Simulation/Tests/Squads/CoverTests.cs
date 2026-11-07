using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Squads.Cover;
using HyperRTS.Simulation.Vision.Sight;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Tests.Squads
{
    /// <summary>
    /// Halted squads take free cover spots facing their way, count as covered only once they stand in them, and take
    /// less from hits inside the spot's front arc. The spot index changes only when cover comes or goes.
    /// </summary>
    public class CoverTests : SimulationFixture
    {
        private static readonly float3 Ahead = new(0f, 0f, 20f);
        private static readonly float3 Behind = new(0f, 0f, -20f);

        private Entity Squad(float3 position, int size) => World.SpawnArmedSquad(position, new byte[size]);

        private Entity Prop(float3 at, params (float3 Position, CoverKind Kind)[] spots)
        {
            var manager = World.EntityManager;
            var prop = manager.CreateEntity(typeof(LocalTransform));
            manager.SetComponentData(prop, LocalTransform.FromPosition(at));
            var buffer = manager.AddBuffer<CoverSpot>(prop);
            foreach (var (position, kind) in spots)
            {
                buffer.Add(new CoverSpot { Position = position, Facing = new float2(0f, 1f), Kind = kind });
            }

            return prop;
        }

        private int BuildCount() =>
            World.EntityManager.CreateEntityQuery(typeof(CoverSpots)).GetSingleton<CoverSpots>().Version;

        private float Lost(Entity squad, int index)
        {
            var member = World.Buffer<SquadMember>(squad)[index];
            return member.MaxHealth - member.Health;
        }

        [Test]
        public void HaltedSquad_TakesTheKindItPrefers_WhateverTheEnumOrder()
        {
            var rules = World.Singleton<CoverRules>();
            var light = rules.Entry(CoverKind.Light);
            light.Preference = rules.Entry(CoverKind.Heavy).Preference + 10f;
            rules.Entries[(int)CoverKind.Light] = light;
            World.Set(World.MatchEntity(), rules);
            Prop(new float3(0f, 0f, 2f), (new float3(-1f, 0f, 0f), CoverKind.Heavy), (new float3(1f, 0f, 0f), CoverKind.Light));
            var squad = Squad(float3.zero, 1);

            World.Run(2f);

            var member = World.Buffer<SquadMember>(squad)[0];
            Assert.AreEqual(1, member.SpotIndex, "the light spot, which is now preferred");
        }

        [Test]
        public void HaltedSquad_TakesFreeSpotsWithoutSharing()
        {
            Prop(new float3(0f, 0f, 2f), (new float3(-1f, 0f, 0f), CoverKind.Heavy), (new float3(0f, 0f, 0f), CoverKind.Light),
                (new float3(1f, 0f, 0f), CoverKind.Heavy));
            var first = Squad(float3.zero, 2);
            var second = Squad(new float3(0.5f, 0f, 0f), 2);

            World.Run(3f);

            var taken = new System.Collections.Generic.HashSet<(Entity, byte)>();
            var holders = 0;
            foreach (var squad in new[] { first, second })
            {
                foreach (var member in World.Buffer<SquadMember>(squad))
                {
                    holders += member.HasSpot ? 1 : 0;
                    Assert.IsTrue(!member.HasSpot || taken.Add((member.SpotEntity, member.SpotIndex)), "a spot is held twice");
                }
            }

            Assert.AreEqual(3, holders, "three spots, four soldiers");
        }

        [Test]
        public void RebuiltIndex_KeepsSpotsHeldAndNoSquadTakesThem()
        {
            Prop(new float3(0f, 0f, 2f), (new float3(-1.5f, 0f, 0f), CoverKind.Heavy), (new float3(-0.5f, 0f, 0f), CoverKind.Heavy),
                (new float3(0.5f, 0f, 0f), CoverKind.Heavy), (new float3(1.5f, 0f, 0f), CoverKind.Heavy));
            var first = Squad(float3.zero, 2);
            var second = Squad(new float3(0.5f, 0f, 0f), 2);
            World.Run(3f);

            foreach (var squad in new[] { first, second })
            {
                var members = World.Buffer<SquadMember>(squad);
                var member = members[0];
                member.LeaveSpot();
                members[0] = member;
            }

            Prop(new float3(0f, 0f, 200f), (float3.zero, CoverKind.Light));
            World.Run(3f);

            var taken = new System.Collections.Generic.HashSet<(Entity, byte)>();
            foreach (var squad in new[] { first, second })
            {
                foreach (var member in World.Buffer<SquadMember>(squad))
                {
                    Assert.IsTrue(member.HasSpot);
                    Assert.IsTrue(taken.Add((member.SpotEntity, member.SpotIndex)), "a spot is held by two members after a rebuild");
                }
            }
        }

        [Test]
        public void Member_IsCoveredAndCrouchedOnlyOnceItStandsInTheSpot()
        {
            Prop(new float3(0f, 0f, 3f), (float3.zero, CoverKind.Heavy));
            var squad = Squad(float3.zero, 1);

            World.Tick();
            World.Tick();
            var walking = World.Buffer<SquadMember>(squad)[0];
            Assert.IsTrue(walking.HasSpot);
            Assert.AreEqual(CoverKind.None, walking.Cover);

            World.Run(3f);
            var arrived = World.Buffer<SquadMember>(squad)[0];
            Assert.AreEqual(CoverKind.Heavy, arrived.Cover);
            Assert.AreEqual(Posture.Crouch, arrived.Posture);
        }

        [Test]
        public void Hit_InsideTheFrontArcIsCutAndOutsideIsNot()
        {
            Prop(new float3(0f, 0f, 3f), (float3.zero, CoverKind.Heavy));
            var squad = Squad(float3.zero, 1);
            World.Run(3f);

            World.HitSquad(squad, 10f, Ahead, suppression: 0.5f);
            World.Tick();
            Assert.AreEqual(5f, Lost(squad, 0), 1e-3f, "heavy cover halves a hit from the front");
            Assert.AreEqual(0.15f, World.Get<Suppression>(squad).Value, 1e-3f, "heavy cover lets 0.3 of the suppression through");

            World.HitSquad(squad, 10f, Behind, suppression: 0.5f);
            World.Tick();
            Assert.AreEqual(15f, Lost(squad, 0), 1e-3f, "a hit from behind is full");
            Assert.AreEqual(0.65f, World.Get<Suppression>(squad).Value, 0.01f);

            World.HitSquad(squad, 2f, Ahead, piercing: 1f);
            World.Tick();
            Assert.AreEqual(17f, Lost(squad, 0), 1e-3f, "a piercing hit ignores cover");

            var burn = World.Own(ScriptableObject.CreateInstance<DamageType>());
            burn.coverPiercing = 1f;
            World.QueueDamage(DamageEvent.Direct(squad, Ahead, Entity.Null, 2, 2f).Typed(DamageProfile.FromType(burn)));
            World.Tick();
            Assert.AreEqual(19f, Lost(squad, 0), 1e-3f, "damage over time of a piercing type ignores cover too");
        }

        [Test]
        public void DirectHits_FavourMembersOutOfCover()
        {
            Prop(new float3(0f, 0f, 1.7f), (new float3(-1.5f, 0f, 0f), CoverKind.Heavy), (new float3(1.5f, 0f, 0f), CoverKind.Heavy));
            var squad = Squad(float3.zero, 4);
            World.Run(3f);

            // Enough small hits that the roll can't by chance favour the covered pair (about five deviations out).
            for (var i = 0; i < 200; i++)
            {
                World.HitSquad(squad, 0.1f, Ahead);
                World.Tick();
            }

            var covered = 0f;
            var exposed = 0f;
            var members = World.Buffer<SquadMember>(squad);
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i].Cover != CoverKind.None)
                {
                    covered += Lost(squad, i);
                }
                else
                {
                    exposed += Lost(squad, i);
                }
            }

            Assert.AreEqual(2, CountCovered(squad));
            Assert.Greater(exposed, covered * 2f, "exposed members were hit more often, and for full damage");
        }

        private int CountCovered(Entity squad)
        {
            var covered = 0;
            foreach (var member in World.Buffer<SquadMember>(squad))
            {
                covered += member.Cover != CoverKind.None ? 1 : 0;
            }

            return covered;
        }

        [Test]
        public void DestroyedProp_DropsTheCover()
        {
            var prop = Prop(new float3(0f, 0f, 3f), (float3.zero, CoverKind.Heavy));
            var squad = Squad(float3.zero, 1);
            World.Run(3f);
            Assert.AreEqual(CoverKind.Heavy, World.Buffer<SquadMember>(squad)[0].Cover);

            World.EntityManager.DestroyEntity(prop);
            World.Tick();

            var member = World.Buffer<SquadMember>(squad)[0];
            Assert.IsFalse(member.HasSpot);
            Assert.AreEqual(CoverKind.None, member.Cover);
        }

        [Test]
        public void SquadInWoods_IsInLightCoverFromEverySideWithoutASpot()
        {
            World.SpawnSightBlock(float3.zero, new SightBlock { Size = new float2(20f, 40f), Density = 0.1f, PorousTop = 10f });
            var squad = Squad(float3.zero, 3);

            World.Run(2f);

            foreach (var member in World.Buffer<SquadMember>(squad))
            {
                Assert.IsFalse(member.HasSpot);
                Assert.AreEqual(CoverKind.Light, member.Cover);
                Assert.AreEqual(Posture.Crouch, member.Posture);
            }

            World.HitSquad(squad, 20f, Behind);
            World.Tick();
            Assert.Less(World.Get<Health>(squad).Current, SquadTestKit.MemberHealth * 3f);
        }

        [Test]
        public void SquadThatMoves_DropsItsSpotsAndStandsUp()
        {
            Prop(new float3(0f, 0f, 3f), (float3.zero, CoverKind.Heavy));
            var squad = Squad(float3.zero, 1);
            World.Run(3f);
            Assert.AreEqual(Posture.Crouch, World.Buffer<SquadMember>(squad)[0].Posture);

            World.MoveTo(squad, new float3(0f, 0f, 40f));
            World.Run(2f);

            var member = World.Buffer<SquadMember>(squad)[0];
            Assert.IsFalse(member.HasSpot);
            Assert.AreEqual(CoverKind.None, member.Cover);
            Assert.AreEqual(Posture.Stand, member.Posture);
        }

        [Test]
        public void Index_BuildsAgainOnlyWhenCoverComesOrGoes()
        {
            Prop(new float3(0f, 0f, 3f), (float3.zero, CoverKind.Heavy));
            World.Run(1f);
            var built = BuildCount();

            World.Run(1f);
            Assert.AreEqual(built, BuildCount(), "nothing changed");

            var second = Prop(new float3(5f, 0f, 3f), (float3.zero, CoverKind.Light));
            World.Tick();
            Assert.AreEqual(built + 1, BuildCount(), "added");

            World.EntityManager.DestroyEntity(second);
            World.Tick();
            Assert.AreEqual(built + 2, BuildCount(), "removed");
        }
    }
}
