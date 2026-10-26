using HyperRTS.Presentation.TeamColors;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.TeamColors
{
    /// <summary>Building lights: every mesh follows the building's power, with a flicker on each change.</summary>
    public class BuildingLightsTests
    {
        private const float Step = 0.05f;

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SystemHandle _system;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Building Lights Tests");
            _em = _kit.EntityManager;
            _system = _kit.World.CreateSystem<BuildingLightsSystem>();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void PoweredBuilding_LightsEveryMesh_AndLeavesOtherPartsAlone()
        {
            Building(OfflineReason.None, out var meshes, out var bare);

            Run(Step);

            Assert.AreEqual(1f, Lights(meshes[0]));
            Assert.AreEqual(1f, Lights(meshes[1]));
            Assert.IsFalse(_em.HasComponent<LightsOn>(bare));
        }

        [Test]
        public void GoingOffline_FlickersThenGoesDark()
        {
            var building = Building(OfflineReason.None, out var meshes, out _);
            Run(Step);

            SetOffline(building, OfflineReason.Unpowered);
            var bright = false;
            var dim = false;
            for (var t = 0f; t < BuildingLights.FlickerSeconds - Step; t += Step)
            {
                Run(Step);
                bright |= Lights(meshes[0]) == 1f;
                dim |= Lights(meshes[0]) is > 0f and < 1f;
            }

            Run(BuildingLights.FlickerSeconds);
            Assert.IsTrue(bright && dim, "flickers before it dies");
            Assert.AreEqual(0f, Lights(meshes[0]));
            Assert.AreEqual(0f, Lights(meshes[1]));
        }

        [Test]
        public void UnderConstruction_StaysDarkUntilComplete_ThenComesOn()
        {
            var building = Building(OfflineReason.UnderConstruction, out var meshes, out _);
            Run(1f);
            Assert.AreEqual(0f, Lights(meshes[0]), "dark from first sight, no flicker");

            SetOffline(building, OfflineReason.None);
            Run(BuildingLights.FlickerSeconds + Step);

            Assert.AreEqual(1f, Lights(meshes[0]));
        }

        [Test]
        public void SiteDressing_KeepsItsWorkLightsOn_WhileTheBuildingIsDark()
        {
            var building = Building(OfflineReason.UnderConstruction, out var meshes, out _);
            var siteMesh = _em.CreateEntity(typeof(MaterialMeshInfo));
            var site = _em.CreateEntity();
            _em.AddBuffer<Child>(site).Add(new Child { Value = siteMesh });
            var group = _em.GetBuffer<LinkedEntityGroup>(building);
            group.Add(site);
            group.Add(siteMesh);
            _em.AddComponentData(building, new ConstructionSite { Visual = site });

            Run(1f);

            Assert.AreEqual(0f, Lights(meshes[0]));
            Assert.IsFalse(_em.HasComponent<LightsOn>(siteMesh));
        }

        private float Lights(Entity mesh) => _em.GetComponentData<LightsOn>(mesh).Value;

        private void SetOffline(Entity building, OfflineReason reasons)
        {
            _em.SetComponentData(building, new Offline { Reasons = reasons });
            _em.SetComponentEnabled<Offline>(building, reasons != OfflineReason.None);
        }

        // Two meshes and a mesh-less part, linked as an instantiated prefab is.
        private Entity Building(OfflineReason reasons, out Entity[] meshes, out Entity bare)
        {
            var root = _em.CreateEntity(typeof(BuildingTag));
            _em.AddComponentData(root, new Offline());
            SetOffline(root, reasons);
            meshes = new[] { _em.CreateEntity(typeof(MaterialMeshInfo)), _em.CreateEntity(typeof(MaterialMeshInfo)) };
            bare = _em.CreateEntity();
            var group = new NativeList<LinkedEntityGroup>(Allocator.Temp) { root, meshes[0], meshes[1], bare };
            _em.AddBuffer<LinkedEntityGroup>(root).AddRange(group.AsArray());
            return root;
        }

        private void Run(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Step)
            {
                _kit.World.SetTime(new TimeData(_kit.World.Time.ElapsedTime + Step, Step));
                _system.Update(_kit.World.Unmanaged);
            }
        }
    }
}
