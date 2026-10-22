using HyperRTS.Presentation.Construction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The construction site: dressing while built, work only while progress rises, one completion beat.</summary>
    public class ConstructionSiteSystemTests
    {
        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SystemHandle _system;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Construction Site Tests");
            _em = _kit.EntityManager;
            _system = _kit.World.GetOrCreateSystem<ConstructionSiteSystem>();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void SiteDressing_ShowsWhileBuilt_AndHidesOnceComplete()
        {
            var building = Building(true, out var site, out var dressing);
            Update(0.0);
            Assert.IsTrue(Shown(site) && Shown(dressing));

            _em.SetComponentEnabled<ConstructionProgress>(building, false);
            Update(0.1);

            Assert.IsFalse(Shown(site) || Shown(dressing));
        }

        [Test]
        public void BuildingFirstSeenComplete_NeverShowsItsSite()
        {
            Building(false, out var site, out var dressing);

            Update(0.0);

            Assert.IsFalse(Shown(site) || Shown(dressing));
        }

        [Test]
        public void Work_RunsWhileProgressRises_AndStopsWhenItStalls()
        {
            var watch = new ConstructionWatch();
            watch.Observe(true, 0.2f, 0.0);
            Assert.IsFalse(watch.IsWorking(0.0), "a site first seen mid-build may be stalled");

            watch.Observe(true, 0.25f, 1.0);
            Assert.IsTrue(watch.IsWorking(1.0));
            watch.Observe(true, 0.25f, 1.4);
            Assert.IsTrue(watch.IsWorking(1.4));
            watch.Observe(true, 0.25f, 1.6);
            Assert.IsFalse(watch.IsWorking(1.6));
        }

        [Test]
        public void Completion_FiresOnce_AndOnlyForAConstructionSeenInProgress()
        {
            var watched = new ConstructionWatch();
            watched.Observe(true, 0.9f, 0.0);
            Assert.IsTrue(watched.Observe(false, 1f, 0.1));
            Assert.IsFalse(watched.Observe(false, 1f, 0.2));
            Assert.IsFalse(watched.IsWorking(0.2));

            var late = new ConstructionWatch();
            Assert.IsFalse(late.Observe(false, 1f, 0.0));
        }

        private void Update(double elapsed)
        {
            _kit.World.SetTime(new TimeData(elapsed, 0.1f));
            _system.Update(_kit.World.Unmanaged);
        }

        private bool Shown(Entity mesh) => _em.IsComponentEnabled<MaterialMeshInfo>(mesh);

        /// <summary>A building whose site child holds a mesh, linked as an instantiated prefab is.</summary>
        private Entity Building(bool underConstruction, out Entity site, out Entity dressing)
        {
            var root = _em.CreateEntity(typeof(HiddenBy), typeof(LocalToWorld), typeof(ConstructionWatch));
            _em.AddComponentData(root, new ConstructionProgress { Value = 0.5f });
            _em.SetComponentEnabled<ConstructionProgress>(root, underConstruction);
            _em.AddBuffer<ConstructionWorkPoint>(root);
            site = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            dressing = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            _em.AddComponentData(dressing, new Parent { Value = site });
            _em.AddComponentData(root, new ConstructionSite { Visual = site });
            var group = new NativeList<LinkedEntityGroup>(Allocator.Temp) { root, site, dressing };
            _em.AddBuffer<LinkedEntityGroup>(root).AddRange(group.AsArray());
            return root;
        }
    }
}
