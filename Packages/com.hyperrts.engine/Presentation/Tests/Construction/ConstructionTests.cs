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

namespace HyperRTS.Presentation.Tests.Construction
{
    /// <summary>Buildings going up: the build reveal on every mesh, and the site dressing with its work and completion beat.</summary>
    public class ConstructionTests
    {
        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SystemHandle _reveal;
        private SystemHandle _site;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Construction Tests");
            _em = _kit.EntityManager;
            _reveal = _kit.World.CreateSystem<BuildRevealSystem>();
            _site = _kit.World.CreateSystem<ConstructionSiteSystem>();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void Reveal_GivesEveryMeshTheRisingProgress_AndOtherPartsNone()
        {
            var building = RevealBuilding(0.3f, true, out var meshes, out var bare);
            _reveal.Update(_kit.World.Unmanaged);

            Assert.AreEqual(0.3f, _em.GetComponentData<BuildProgress>(meshes[0]).Value);
            Assert.AreEqual(0.3f, _em.GetComponentData<BuildProgress>(meshes[1]).Value);
            Assert.IsFalse(_em.HasComponent<BuildProgress>(bare));
            Assert.IsTrue(_em.HasComponent<BuildRevealing>(building));

            _em.SetComponentData(building, new ConstructionProgress { Value = 0.6f });
            _reveal.Update(_kit.World.Unmanaged);

            Assert.AreEqual(0.6f, _em.GetComponentData<BuildProgress>(meshes[1]).Value);
        }

        [Test]
        public void Reveal_CompletionTakesTheProgressOff_SoTheModelDrawsAsAuthored()
        {
            var building = RevealBuilding(0.9f, true, out var meshes, out _);
            _reveal.Update(_kit.World.Unmanaged);

            _em.SetComponentEnabled<ConstructionProgress>(building, false);
            _reveal.Update(_kit.World.Unmanaged);

            Assert.IsFalse(_em.HasComponent<BuildProgress>(meshes[0]));
            Assert.IsFalse(_em.HasComponent<BuildProgress>(meshes[1]));
            Assert.IsFalse(_em.HasComponent<BuildRevealing>(building));
        }

        [Test]
        public void Reveal_FinishedBuildingNeverGetsTheProgress()
        {
            var building = RevealBuilding(1f, false, out var meshes, out _);

            _reveal.Update(_kit.World.Unmanaged);

            Assert.IsFalse(_em.HasComponent<BuildProgress>(meshes[0]));
            Assert.IsFalse(_em.HasComponent<BuildRevealing>(building));
        }

        [Test]
        public void SiteDressing_ShowsWhileBuilt_AndHidesOnceComplete()
        {
            var building = SiteBuilding(true, out var site, out var dressing);
            UpdateSite(0.0);
            Assert.IsTrue(Shown(site) && Shown(dressing));

            _em.SetComponentEnabled<ConstructionProgress>(building, false);
            UpdateSite(0.1);

            Assert.IsFalse(Shown(site) || Shown(dressing));
        }

        [Test]
        public void Watch_StopsOnceBuilt_AndResumesWithConstruction()
        {
            var building = SiteBuilding(true, out var site, out _);
            UpdateSite(0.0);
            _em.SetComponentEnabled<ConstructionProgress>(building, false);
            UpdateSite(0.1);
            Assert.IsFalse(_em.IsComponentEnabled<ConstructionWatch>(building));

            _em.SetComponentEnabled<ConstructionProgress>(building, true);
            UpdateSite(0.2);

            Assert.IsTrue(_em.IsComponentEnabled<ConstructionWatch>(building));
            Assert.IsTrue(Shown(site));
        }

        [Test]
        public void BuildingFirstSeenComplete_NeverShowsItsSite()
        {
            SiteBuilding(false, out var site, out var dressing);

            UpdateSite(0.0);

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

        private void UpdateSite(double elapsed)
        {
            _kit.World.SetTime(new TimeData(elapsed, 0.1f));
            _site.Update(_kit.World.Unmanaged);
        }

        private bool Shown(Entity mesh) => _em.IsComponentEnabled<MaterialMeshInfo>(mesh);

        // Two meshes and a mesh-less part, linked as an instantiated prefab is.
        private Entity RevealBuilding(float progress, bool building, out Entity[] meshes, out Entity bare)
        {
            var root = _em.CreateEntity();
            _em.AddComponentData(root, new ConstructionProgress { Value = progress });
            _em.SetComponentEnabled<ConstructionProgress>(root, building);
            meshes = new[] { _em.CreateEntity(typeof(MaterialMeshInfo)), _em.CreateEntity(typeof(MaterialMeshInfo)) };
            bare = _em.CreateEntity();
            var group = new NativeList<LinkedEntityGroup>(Allocator.Temp) { root, meshes[0], meshes[1], bare };
            _em.AddBuffer<LinkedEntityGroup>(root).AddRange(group.AsArray());
            return root;
        }

        // A site child holding a dressing mesh, linked as an instantiated prefab is.
        private Entity SiteBuilding(bool underConstruction, out Entity site, out Entity dressing)
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
