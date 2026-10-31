using HyperRTS.Presentation.Environment;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment
{
    /// <summary>
    /// Field fires: what lights them, the downwind lean, burning out for good, one seed burning one way, and showing only
    /// on ground the local player sees.
    /// </summary>
    public class FieldFireTests
    {
        private const float Frame = 1f / 30f;
        private const uint Seed = 0xA341316Cu;
        private static readonly float2 BurnSeconds = new(20f, 60f);

        // Far past every test field, for the tests about spread rather than reach.
        private static readonly float2 Unbounded = new(1000f, 1000f);

        [Test]
        public void Ignition_OnlyBigBlastsOnDryGroundUnderTheWetnessLimit()
        {
            var host = new GameObject("Field Fires");
            try
            {
                var settings = host.AddComponent<FieldFireSettings>();
                settings.igniteChance = 1f;
                var shell = new CueEvent { Slot = CueSlot.Impact, Radius = 3f, Surface = ImpactSurface.Ground };
                var bullet = shell;
                bullet.Radius = 0.2f;
                var onRoof = shell;
                onRoof.Surface = ImpactSurface.Structure;

                Assert.IsTrue(settings.Lights(shell, 0.1f, Seed), "a shell on dry ground");
                Assert.IsFalse(settings.Lights(shell, 0.3f, Seed), "wet ground at the limit");
                Assert.IsFalse(settings.Lights(bullet, 0.1f, Seed), "no high explosive");
                Assert.IsFalse(settings.Lights(onRoof, 0.1f, Seed), "a building, not the ground");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            var fires = Field(new int2(10, 10), cell => cell.x >= 5);
            Assert.IsFalse(fires.TryIgnite(new float2(2.5f, 5.5f), Seed), "bare ground");
            Assert.IsTrue(fires.TryIgnite(new float2(7.5f, 5.5f), Seed), "dry ground");
            Assert.IsFalse(fires.TryIgnite(new float2(7.5f, 5.5f), Seed), "already burning");
            fires.MarkBare(new float2(8.5f, 8.5f), 1f);
            Assert.IsFalse(fires.TryIgnite(new float2(8.5f, 8.5f), Seed), "cleared round a building");
        }

        [Test]
        public void Wind_LeansTheSpreadDownwind()
        {
            var fires = Field(new int2(120, 120), _ => true);
            fires.TryIgnite(new float2(60.5f, 60.5f), Seed);

            Run(fires, 30f, new float2(2f, 0f));

            int east = 0, west = 0;
            ForEach(fires, (cell, state) =>
            {
                var lit = state != FieldCell.Dry;
                east += lit && cell.x > 60 ? 1 : 0;
                west += lit && cell.x < 60 ? 1 : 0;
            });
            Assert.Greater(east, west * 2, "the front ran east with the wind");
        }

        [Test]
        public void Cells_BurnOutAndStayBurnt_AndBareGroundStopsTheFront()
        {
            var fires = Field(new int2(40, 20), cell => cell.x != 20);
            fires.TryIgnite(new float2(5.5f, 10.5f), Seed);

            Run(fires, 400f, float2.zero);

            Assert.AreEqual(0, fires.Burning, "every cell burnt out");
            ForEach(fires, (cell, state) =>
            {
                var side = cell.x < 20 ? FieldCell.Burnt : FieldCell.Dry;
                Assert.AreEqual(cell.x == 20 ? FieldCell.Bare : side, state, $"cell {cell}");
            });
            Assert.IsFalse(fires.TryIgnite(new float2(5.5f, 10.5f), Seed), "burnt ground doesn't burn again");
        }

        [Test]
        public void EachFire_DiesOutInsideItsReach()
        {
            var ignition = new float2(60.5f, 60.5f);
            var fires = Field(new int2(120, 120), _ => true, new float2(5f, 15f));
            fires.TryIgnite(ignition, Seed);

            Run(fires, 400f, new float2(1.5f, 0f));

            Assert.AreEqual(0, fires.Burning, "the front died out by itself");
            var burnt = 0;
            ForEach(fires, (cell, state) =>
            {
                if (state == FieldCell.Burnt)
                {
                    burnt++;
                    Assert.Less(math.distance(fires.Centre(cell), ignition), 15f, $"cell {cell} past the reach");
                }
            });
            Assert.Less(burnt, (int)(math.PI * 15f * 15f), "a blotch, not the field");
        }

        [Test]
        public void Fronts_StopAtABuildingsClearing()
        {
            var building = new float2(40f, 40f);
            var fires = Field(new int2(80, 80), _ => true);
            fires.MarkBare(building, 12f);
            fires.TryIgnite(new float2(20.5f, 40.5f), Seed);

            Run(fires, 300f, new float2(2f, 0f));

            ForEach(fires, (cell, state) =>
            {
                var inClearing = math.distance(fires.Centre(cell), building) <= 12f;
                Assert.IsFalse(inClearing && state != FieldCell.Bare, $"cell {cell} burnt in the clearing");
            });
            Assert.AreEqual(FieldCell.Burnt, fires.At(new float2(60.5f, 40.5f)), "the front went round it");
        }

        [Test]
        public void SameImpact_BurnsTheSameFire()
        {
            var first = Field(new int2(60, 60), _ => true);
            var second = Field(new int2(60, 60), _ => true);
            var other = Field(new int2(60, 60), _ => true);
            var wind = new float2(0.7f, 0.7f);
            first.TryIgnite(new float2(30.5f, 30.5f), Seed);
            second.TryIgnite(new float2(30.5f, 30.5f), Seed);
            other.TryIgnite(new float2(30.5f, 30.5f), Seed + 1u);

            Run(first, 40f, wind);
            Run(second, 40f, wind);
            Run(other, 40f, wind);

            var differs = false;
            ForEach(first, (cell, state) =>
            {
                var centre = first.Centre(cell);
                Assert.AreEqual(state, second.At(centre), $"cell {cell}");
                differs |= state != other.At(centre);
            });
            Assert.IsTrue(differs, "another impact burns another way");
        }

        [Test]
        public void Fires_ShowOnlyOnGroundTheLocalPlayerSees()
        {
            // Two 10 m fog cells; team 1 sees only the west one.
            var visible = new NativeArray<ushort>(2, Allocator.Temp);
            visible[0] = 1 << 1;
            var fog = new FogOfWar { Visible = visible, Size = new int2(2, 1), CellSize = 10f };
            var view = new LocalFogView { Viewer = 1, Team = 1, Active = true };
            try
            {
                Assert.IsTrue(CueRules.IsGroundSeenLocally(new float3(5f, 0f, 5f), view, fog), "in sight");
                Assert.IsFalse(CueRules.IsGroundSeenLocally(new float3(15f, 0f, 5f), view, fog), "in fog");
                view.Active = false;
                Assert.IsTrue(CueRules.IsGroundSeenLocally(new float3(15f, 0f, 5f), view, fog), "fog view off");
            }
            finally
            {
                visible.Dispose();
            }
        }

        [Test]
        public void Ignition_BetweenStepsFillsUpToTheCap()
        {
            var fires = Field(new int2(100, 100), _ => true);
            fires.TryIgnite(new float2(50.5f, 50.5f), Seed);
            Run(fires, 10f, float2.zero);

            ForEach(fires, (cell, _) => fires.TryIgnite(fires.Centre(cell), Seed));

            var burning = 0;
            ForEach(fires, (_, state) => burning += state == FieldCell.Burning ? 1 : 0);
            Assert.AreEqual(FieldFires.MaxBurning, burning, "the last step's spread counts once");
        }

        private static FieldFires Field(int2 size, System.Func<int2, bool> dry) => Field(size, dry, Unbounded);

        private static FieldFires Field(int2 size, System.Func<int2, bool> dry, float2 reach)
        {
            var fires = new FieldFires(float2.zero, size, BurnSeconds, reach);
            for (var z = 0; z < size.y; z++)
            {
                for (var x = 0; x < size.x; x++)
                {
                    if (dry(new int2(x, z)))
                    {
                        fires.MarkDry(new int2(x, z));
                    }
                }
            }

            return fires;
        }

        private static void Run(FieldFires fires, float seconds, float2 wind)
        {
            for (var time = 0f; time < seconds; time += Frame)
            {
                fires.Advance(Frame, wind);
            }
        }

        private static void ForEach(FieldFires fires, System.Action<int2, FieldCell> visit)
        {
            for (var z = 0; z < fires.Size.y; z++)
            {
                for (var x = 0; x < fires.Size.x; x++)
                {
                    var cell = new int2(x, z);
                    visit(cell, fires.At(fires.Centre(cell)));
                }
            }
        }
    }
}
