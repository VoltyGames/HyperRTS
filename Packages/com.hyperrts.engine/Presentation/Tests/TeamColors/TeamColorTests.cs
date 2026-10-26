using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.TeamColors;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.TeamColors
{
    /// <summary>Team colours: tinting owned meshes and strip bands as owners and players change, and the colours the HUD reads.</summary>
    public class TeamColorTests
    {
        private static readonly float4 Blue = new(0f, 0f, 1f, 1f);
        private static readonly float4 Red = new(1f, 0f, 0f, 1f);
        private static readonly float4 Green = new(0f, 1f, 0f, 1f);

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SystemHandle _teamColor;
        private EntityCommandBufferSystem _commands;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Team Color Tests");
            _em = _kit.EntityManager;
            _commands = _kit.World.GetOrCreateSystemManaged<BeginPresentationEntityCommandBufferSystem>();
            _teamColor = _kit.World.CreateSystem<TeamColorSystem>();
            _kit.AddPlayer(1, Blue, local: true);
            _kit.AddPlayer(2, Red);
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void OwnedMeshesAndChildren_AreTinted_NeutralLeftAlone()
        {
            var unit = Spawn(1, out var child);
            var neutral = Spawn(Faction.Neutral, out _);

            Run();

            Assert.AreEqual(Blue, Tint(unit));
            Assert.AreEqual(Blue, Tint(child));
            Assert.IsFalse(_em.HasComponent<TeamColor>(neutral));
            Assert.IsFalse(_em.HasComponent<TeamStrip>(unit), "no band: the mask alone decides");
        }

        [Test]
        public void GoingNeutral_DropsTheTint_AndANewOwnerTintsAgain()
        {
            var building = Spawn(1, out var child);
            Run();

            SetOwner(building, Faction.Neutral);
            Run();
            Assert.IsFalse(_em.HasComponent<TeamColor>(building));
            Assert.IsFalse(_em.HasComponent<TeamColor>(child), "back to its authored look");

            SetOwner(building, 2);
            Run();
            Assert.AreEqual(Red, Tint(child));
        }

        [Test]
        public void StripBand_IsCopiedFromTheRoot_AndDroppedWhenNeutral()
        {
            var building = Spawn(1, out var child);
            _em.AddComponentData(building, new TeamStripBand { Bottom = 2f, Top = 2.4f });
            Run();
            Assert.AreEqual(new float4(2f, 2.4f, 0f, 0f), _em.GetComponentData<TeamStrip>(child).Value);

            SetOwner(building, Faction.Neutral);
            Run();

            Assert.IsFalse(_em.HasComponent<TeamStrip>(child));
        }

        [Test]
        public void NewOwner_WaitsForItsPlayer()
        {
            var unit = Spawn(3, out _);
            Run();
            Assert.IsFalse(_em.HasComponent<TeamColor>(unit), "no player 3 yet");

            _em.AddComponentData(_em.CreateEntity(), new Player { Faction = 3, Color = Green });
            Run();

            Assert.AreEqual(Green, Tint(unit));
        }

        [Test]
        public void Capture_FollowsTheOwner_EvenWhenItsPlayerArrivesLate()
        {
            var unit = Spawn(2, out _);
            Run();

            SetOwner(unit, 1);
            Run();
            Assert.AreEqual(Blue, Tint(unit));

            SetOwner(unit, 3);
            Run();
            _em.AddComponentData(_em.CreateEntity(), new Player { Faction = 3, Color = Green });
            Run();

            Assert.AreEqual(Green, Tint(unit));
        }

        [Test]
        public void HUDColors_ComeFromPlayers_UntilAnOverrideReplacesThem()
        {
            var view = new MatchView();
            view.Refresh(_kit.World);
            AssertColor(view, Color.red, 2);

            var colorOverride = new TeamColorOverride();
            colorOverride.Colors.Add(new FactionColor { Faction = 2, Color = Green });
            var singleton = _em.CreateSingleton(colorOverride);
            view.Refresh(_kit.World);
            AssertColor(view, Color.green, 2);
            AssertColor(view, Color.blue, 1, "factions the override doesn't list keep their colour");

            _em.DestroyEntity(singleton);
            view.Refresh(_kit.World);
            AssertColor(view, Color.red, 2);
        }

        // Colours go through linear-to-gamma, so Color's approximate equality compares them.
        private static void AssertColor(MatchView view, Color expected, byte faction, string message = null) =>
            Assert.IsTrue(expected == view.GetColor(faction), message ?? $"faction {faction}: {view.GetColor(faction)}");

        // An owned root with one child mesh in its group, as baking leaves it.
        private Entity Spawn(byte faction, out Entity child)
        {
            var root = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(EntityInfo));
            _em.AddComponentData(root, new Faction { Value = faction });
            _em.AddComponentData(root, new LocalToWorld { Value = float4x4.identity });
            child = _em.CreateEntity(typeof(MaterialMeshInfo));
            _em.AddBuffer<LinkedEntityGroup>(root).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { root, child }, Allocator.Temp));
            return root;
        }

        private void SetOwner(Entity root, byte faction) => _em.SetComponentData(root, new Faction { Value = faction });

        private float4 Tint(Entity entity) => _em.GetComponentData<TeamColor>(entity).Value;

        private void Run()
        {
            _teamColor.Update(_kit.World.Unmanaged);
            _commands.Update();
            _em.CompleteAllTrackedJobs();
        }
    }
}
