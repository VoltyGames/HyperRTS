using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Presentation.TeamColors;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    public class PresentationSystemTests
    {
        private static readonly float4 Blue = new(0f, 0f, 1f, 1f);
        private static readonly float4 Red = new(1f, 0f, 0f, 1f);

        private PresentationWorldKit _kit;
        private World _world;
        private EntityManager _em;
        private SystemHandle _teamColor;
        private SystemHandle _fog;
        private EntityCommandBufferSystem _commands;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Presentation Tests");
            _world = _kit.World;
            _em = _kit.EntityManager;
            _commands = _world.GetOrCreateSystemManaged<BeginPresentationEntityCommandBufferSystem>();
            _teamColor = _world.CreateSystem<TeamColorSystem>();
            _fog = _world.CreateSystem<FogVisibilitySystem>();
            _kit.AddPlayer(1, Blue, local: true);
            _kit.AddPlayer(2, Red);
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void TeamColor_TintsOwnedMeshesAndChildren_LeavesNeutralAlone()
        {
            var unit = Spawn(1, float3.zero);
            var child = _em.CreateEntity(typeof(MaterialMeshInfo));
            _em.AddBuffer<LinkedEntityGroup>(unit).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { unit, child }, Allocator.Temp));
            var neutral = Spawn(Faction.Neutral, float3.zero);

            Run(_teamColor);

            Assert.AreEqual(Blue, _em.GetComponentData<TeamColor>(unit).Value);
            Assert.AreEqual(Blue, _em.GetComponentData<TeamColor>(child).Value);
            Assert.IsFalse(_em.HasComponent<TeamColor>(neutral));
            Assert.IsFalse(_em.HasComponent<TeamStrip>(unit), "no band: the mask alone decides");
        }

        [Test]
        public void TeamColor_FollowsOwnerChange()
        {
            var unit = Spawn(2, float3.zero);
            Run(_teamColor);

            _em.SetComponentData(unit, new Faction { Value = 1 });
            Run(_teamColor);

            Assert.AreEqual(Blue, _em.GetComponentData<TeamColor>(unit).Value);
        }

        [Test]
        public void TeamColor_DropsTheTintWhenTheOwnerBecomesNeutral()
        {
            var building = Spawn(1, float3.zero);
            var child = _em.CreateEntity(typeof(MaterialMeshInfo));
            _em.AddBuffer<LinkedEntityGroup>(building).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { building, child }, Allocator.Temp));
            Run(_teamColor);
            Assert.AreEqual(Blue, _em.GetComponentData<TeamColor>(child).Value);

            _em.SetComponentData(building, new Faction { Value = Faction.Neutral });
            Run(_teamColor);

            Assert.IsFalse(_em.HasComponent<TeamColor>(building));
            Assert.IsFalse(_em.HasComponent<TeamColor>(child), "back to its authored look");

            _em.SetComponentData(building, new Faction { Value = 2 });
            Run(_teamColor);
            Assert.AreEqual(Red, _em.GetComponentData<TeamColor>(child).Value, "a new owner tints it again");
        }

        [Test]
        public void TeamColor_CopiesTheRootsStripBand_AndDropsItWhenNeutral()
        {
            var building = Spawn(1, float3.zero);
            _em.AddComponentData(building, new TeamStripBand { Bottom = 2f, Top = 2.4f });
            var child = _em.CreateEntity(typeof(MaterialMeshInfo));
            _em.AddBuffer<LinkedEntityGroup>(building).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { building, child }, Allocator.Temp));
            Run(_teamColor);
            Assert.AreEqual(new float4(2f, 2.4f, 0f, 0f), _em.GetComponentData<TeamStrip>(child).Value);

            _em.SetComponentData(building, new Faction { Value = Faction.Neutral });
            Run(_teamColor);

            Assert.IsFalse(_em.HasComponent<TeamStrip>(child));
            Assert.IsFalse(_em.HasComponent<TeamColor>(child));
        }

        [Test]
        public void TeamColor_WaitsForTheOwnersPlayer()
        {
            var unit = Spawn(3, float3.zero);
            Run(_teamColor);
            Assert.IsFalse(_em.HasComponent<TeamColor>(unit), "no player 3 yet");

            _em.AddComponentData(_em.CreateEntity(), new Player { Faction = 3, Color = Blue });
            Run(_teamColor);

            Assert.AreEqual(Blue, _em.GetComponentData<TeamColor>(unit).Value);
        }

        [Test]
        public void TeamColor_RecolorsWhenACapturersPlayerArrivesLate()
        {
            var unit = Spawn(1, float3.zero);
            Run(_teamColor);

            _em.SetComponentData(unit, new Faction { Value = 3 });
            Run(_teamColor);
            _em.AddComponentData(_em.CreateEntity(), new Player { Faction = 3, Color = Red });
            Run(_teamColor);

            Assert.AreEqual(Red, _em.GetComponentData<TeamColor>(unit).Value);
        }

        [Test]
        public void Fog_StopsRenderingHiddenRoots_UntilTheyAreRevealed()
        {
            var enemy = Spawn(2, float3.zero);
            var child = Part(enemy);
            var own = Spawn(1, float3.zero);
            _em.AddComponent<FogHidden>(enemy);

            Run(_fog);
            Assert.IsFalse(Drawn(enemy));
            Assert.IsFalse(Drawn(child));
            Assert.IsTrue(Drawn(own));

            _em.RemoveComponent<FogHidden>(enemy);
            Run(_fog);
            Assert.IsTrue(Drawn(enemy));
            Assert.IsTrue(Drawn(child));
        }

        [Test]
        public void Fog_HidesAPassengerInside_AndShowsItAgainOnceOut()
        {
            var passenger = Spawn(1, float3.zero);
            var child = Part(passenger);
            _em.AddComponentData(passenger, new Inside { Container = Entity.Null });

            Run(_fog);
            Assert.IsFalse(Drawn(child), "aboard: not drawn");

            _em.SetComponentEnabled<Inside>(passenger, false);
            Run(_fog);
            Assert.IsTrue(Drawn(passenger));
            Assert.IsTrue(Drawn(child), "out again: drawn");
        }

        [Test]
        public void Fog_Reveal_KeepsAPartHiddenForAnotherReason()
        {
            var enemy = Spawn(2, float3.zero);
            var stage = Part(enemy);
            _em.SetComponentData(stage, new HiddenBy { Reasons = HideReason.Stage });
            _em.SetComponentEnabled<MaterialMeshInfo>(stage, false);
            _em.AddComponent<FogHidden>(enemy);
            Run(_fog);

            _em.RemoveComponent<FogHidden>(enemy);
            Run(_fog);
            Assert.IsTrue(Drawn(enemy));
            Assert.IsFalse(Drawn(stage), "still not the stage on screen");
        }

        /// <summary>A mesh in <paramref name="root"/>'s group, as baking leaves it.</summary>
        private Entity Part(Entity root)
        {
            var part = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            _em.AddBuffer<LinkedEntityGroup>(root).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { root, part }, Allocator.Temp));
            return part;
        }

        private bool Drawn(Entity entity) => _em.IsComponentEnabled<MaterialMeshInfo>(entity);

        private Entity Spawn(byte faction, float3 position)
        {
            var entity = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(EntityInfo), typeof(HiddenBy));
            _em.AddComponentData(entity, new Faction { Value = faction });
            _em.AddComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });
            return entity;
        }

        private void Run(SystemHandle system)
        {
            system.Update(_world.Unmanaged);
            _commands.Update();
            _em.CompleteAllTrackedJobs();
        }
    }
}
