using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.TeamColors;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Owner colours the HUD reads, with and without a <see cref="TeamColorOverride"/>.</summary>
    public class MatchViewColorTests
    {
        private static readonly float4 Red = new(1f, 0f, 0f, 1f);
        private static readonly float4 Green = new(0f, 1f, 0f, 1f);
        private static readonly float4 Blue = new(0f, 0f, 1f, 1f);

        private World _world;
        private EntityManager _em;
        private MatchView _view;

        [SetUp]
        public void SetUp()
        {
            _world = new World("MatchView Colour Tests");
            _em = _world.EntityManager;
            var local = _em.CreateEntity(typeof(LocalPlayer));
            _em.AddComponentData(local, new Player { Faction = 1, Color = Red });
            _em.AddComponentData(_em.CreateEntity(), new Player { Faction = 2, Color = Green });
            _view = new MatchView();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void Colors_ComeFromPlayers_UntilAnOverrideReplacesThem()
        {
            _view.Refresh(_world);
            AssertColor(Color.green, 2);

            var colorOverride = new TeamColorOverride();
            colorOverride.Colors.Add(new FactionColor { Faction = 2, Color = Blue });
            var singleton = _em.CreateSingleton(colorOverride);
            _view.Refresh(_world);
            AssertColor(Color.blue, 2);
            AssertColor(Color.red, 1, "factions the override doesn't list keep their colour");

            _em.DestroyEntity(singleton);
            _view.Refresh(_world);
            AssertColor(Color.green, 2);
        }

        // Colours go through linear-to-gamma, so compare approximately.
        private void AssertColor(Color expected, byte faction, string message = null) =>
            Assert.IsTrue(expected == _view.ColorOf(faction), message ?? $"faction {faction}: {_view.ColorOf(faction)}");
    }
}
