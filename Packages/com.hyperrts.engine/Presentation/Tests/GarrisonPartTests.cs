using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Transport;
using NUnit.Framework;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Garrison parts draw only while one of their passenger types is aboard their container.</summary>
    public class GarrisonPartTests
    {
        private const int Gunners = 5;
        private const int Missileers = 9;

        private World _world;
        private EntityManager _em;
        private SystemHandle _system;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Garrison Part Tests");
            _em = _world.EntityManager;
            _system = _world.CreateSystem<GarrisonPartSystem>();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Part_ShowsOnlyForItsPassengers()
        {
            var bunker = _em.CreateEntity();
            _em.AddBuffer<Cargo>(bunker);
            var gun = Part(bunker, Gunners);

            Run();
            Assert.IsFalse(Drawn(gun), "empty");

            Board(bunker, Missileers);
            Run();
            Assert.IsFalse(Drawn(gun), "the wrong passengers");

            Board(bunker, Gunners);
            Run();
            Assert.IsTrue(Drawn(gun), "its passengers aboard");

            _em.GetBuffer<Cargo>(bunker).Clear();
            Run();
            Assert.IsFalse(Drawn(gun), "emptied again");
        }

        private Entity Part(Entity container, int typeId)
        {
            var part = _em.CreateEntity();
            _em.AddComponentData(part, new HiddenBy());
            _em.AddComponentData(part, new MaterialMeshInfo());
            var writer = new EntityManagerWriter(_em, part);
            GarrisonPartAuthoring.Add(ref writer, container, new[] { typeId });
            return part;
        }

        private void Board(Entity container, int typeId)
        {
            var unit = _em.CreateEntity();
            _em.AddComponentData(unit, new EntityInfo { TypeId = typeId });
            _em.GetBuffer<Cargo>(container).Add(new Cargo { Unit = unit, Size = 1 });
        }

        private bool Drawn(Entity part) => _em.IsComponentEnabled<MaterialMeshInfo>(part);

        private void Run()
        {
            _system.Update(_world.Unmanaged);
            _em.CompleteAllTrackedJobs();
        }
    }
}
