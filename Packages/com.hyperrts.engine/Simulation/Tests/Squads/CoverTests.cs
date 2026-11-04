using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Squads
{
    /// <summary>
    /// Halted squads take free cover spots facing their way, count as covered only once they stand in them, and take
    /// less from hits inside the spot's front arc. The spot index changes only when cover comes or goes.
    /// </summary>
    public class CoverTests : SimulationFixture
    {
        private const float Each = 25f;
        private static readonly float3 Ahead = new(0f, 0f, 20f);
        private static readonly float3 Behind = new(0f, 0f, -20f);

        private Entity Squad(float3 position, int size)
        {
            var squad = World.SpawnUnit(1, position, maxHealth: Each * size);
            SquadAuthoring.Add(World.Writer(squad), Each * size, new byte[size]);
            World.AddWeapon(squad, range: 6f, damage: 10f, cooldown: 100f);
            return squad;
        }

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

        private void Hit(Entity target, float amount, float3 origin, float piercing = 0f) =>
            World.QueueDamage(new DamageEvent
            {
                Target = target, Position = origin, Origin = origin, SourceFaction = 2, Amount = amount,
                EdgeFactor = 1f, CoverPiercing = piercing,
            });

        private int BuildCount() =>
            World.EntityManager.CreateEntityQuery(typeof(CoverSpots)).GetSingleton<CoverSpots>().Version;

        private float Lost(Entity squad, int index)
        {
            var member = World.Buffer<SquadMember>(squad)[index];
            return member.MaxHealth - member.Health;
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

            Hit(squad, 10f, Ahead);
            World.Tick();
            Assert.AreEqual(5f, Lost(squad, 0), 1e-3f, "heavy cover halves a hit from the front");

            Hit(squad, 10f, Behind);
            World.Tick();
            Assert.AreEqual(15f, Lost(squad, 0), 1e-3f, "a hit from behind is full");

            Hit(squad, 2f, Ahead, piercing: 1f);
            World.Tick();
            Assert.AreEqual(17f, Lost(squad, 0), 1e-3f, "a piercing hit ignores cover");
        }

        [Test]
        public void DirectHits_FavourMembersOutOfCover()
        {
            Prop(new float3(0f, 0f, 1.7f), (new float3(-1.5f, 0f, 0f), CoverKind.Heavy), (new float3(1.5f, 0f, 0f), CoverKind.Heavy));
            var squad = Squad(float3.zero, 4);
            World.Run(3f);

            for (var i = 0; i < 40; i++)
            {
                Hit(squad, 1f, Ahead);
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
