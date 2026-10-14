using System;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests
{
    public class FlagSetupTests
    {
        private World _world;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Flag Setup Tests");
            _em = _world.EntityManager;
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Pole_DropsTheFlagByThePoleBeyondTwoFlagHeights()
        {
            var building = Mount(1f, 5f);

            Assert.AreEqual(3f, _em.GetComponentData<FlagMount>(building).Drop, 1e-5f);
        }

        [TestCase(1.5f, TestName = "Pole shorter than two flag heights")]
        [TestCase(11f, TestName = "Pole past the longest drop")]
        public void Pole_OutOfRange_Throws(float poleHeight)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Mount(1f, poleHeight));
        }

        private Entity Mount(float flagHeight, float poleHeight)
        {
            var building = _em.CreateEntity();
            var writer = new EntityManagerWriter(_em, building);
            FlagMountAuthoring.AddFlagMount(ref writer, _em.CreateEntity(), flagHeight, poleHeight);
            return building;
        }
    }
}
