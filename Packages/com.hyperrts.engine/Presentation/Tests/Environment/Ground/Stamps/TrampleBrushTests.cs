using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Presentation.Environment.Ground.Stamps;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Cues.Effects;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment.Ground.Stamps
{
    /// <summary>The brush drawn into real render targets and read back as the shaders read them.</summary>
    public class TrampleBrushTests
    {
        private const string ProbePath = "Packages/com.hyperrts.engine/Presentation/Tests/Environment/Ground/Stamps/TrampleProbe.compute";

        private static readonly Vector4 Red = new(1f, 0f, 0f, 0f);
        private static readonly Vector4 Blue = new(0f, 0f, 1f, 0f);
        private static readonly Vector4 Alpha = new(0f, 0f, 0f, 1f);
        private static readonly float4 Points = new(50f, 120f, 50f, -120f);

        private ComputeShader _probe;
        private TrampleSurfaces _surfaces;
        private TrampleBrush _brush;
        private ImpactMark _mark;

        [SetUp]
        public void SetUp()
        {
            _probe = AssetDatabase.LoadAssetAtPath<ComputeShader>(ProbePath);
            _surfaces = new TrampleSurfaces(new float2(-200f, -200f), new float2(400f), TrampleQuality.Full);
            _brush = new TrampleBrush();
            _brush.Clear(_surfaces);
            _mark = ScriptableObject.CreateInstance<ImpactMark>();
            _mark.scar.craterDepth = 1f;
            _mark.scar.scorch = 1f;
            _mark.scar.burnReach = 1.5f;
        }

        [TearDown]
        public void TearDown()
        {
            _brush.Dispose();
            _surfaces.Dispose();
            Object.DestroyImmediate(_mark);
        }

        // Render targets flip rows on some graphics APIs: a stamp must read back where it was drawn, not mirrored.
        [Test]
        public void Stamp_IsSampledWhereItWasDrawn()
        {
            var stamps = new TrampleStamps();
            stamps.Tracks.Add(new TrampleStamp
            {
                Center = new float2(50f, 120f), Forward = new float2(0f, 1f), HalfSize = new float2(3f), Value = new float4(1f, 1f, 0f, 0f),
            });
            Draw(stamps, 0, 0);

            var samples = Sample(_surfaces.Tracks, Red);

            Assert.Greater(samples[0], 0.5f, "no rut where the stamp was drawn");
            Assert.Less(samples[1], 0.01f, "the stamp shows up mirrored across the map");
        }

        [Test]
        public void Ruts_DeepenWithEachPass_UpToTheCap()
        {
            var lone = TrampleStamps.RutLoneShare;
            var pass = new TrampleStamp
            {
                Center = new float2(50f, 120f), Forward = new float2(0f, 1f), HalfSize = new float2(3f),
                Value = new float4(lone, 1f, lone * TrampleStamps.RutDeepening, 0f), Rim = 1f,
            };

            DrawPass(pass);
            Assert.AreEqual(lone, Sample(_surfaces.Tracks, Red)[0], 2f / 255f, "a lone pass is not stored at its share");

            DrawPass(pass);
            Assert.AreEqual(lone * (1f + TrampleStamps.RutDeepening), Sample(_surfaces.Tracks, Red)[0], 2f / 255f, "a second pass did not deepen");

            for (var i = 0; i < 20; i++)
            {
                DrawPass(pass);
            }

            Assert.AreEqual(lone * TrampleStamps.RutCap, Sample(_surfaces.Tracks, Red)[0], 2f / 255f, "the rut went past its cap");

            var light = pass;
            light.Value = new float4(lone * 0.3f, 1f, lone * 0.3f * TrampleStamps.RutDeepening, 0f);
            DrawPass(light);
            Assert.AreEqual(lone * TrampleStamps.RutCap, Sample(_surfaces.Tracks, Red)[0], 2f / 255f, "a light vehicle made a deep rut shallower");
        }

        [Test]
        public void Scars_OutlastTheFade_WhileRutsFade()
        {
            Assert.AreEqual(TrampleStamps.GroundLevelBias, Sample(_surfaces.Tracks, Alpha)[0], 1e-3f, "cleared ground is not level");
            var stamps = new TrampleStamps();
            stamps.AddScar(new float2(50f, 120f), _mark.scar, 6f * 0.5f);
            Draw(stamps, 0, 0);
            var bowl = Sample(_surfaces.Tracks, Alpha)[0];
            var scorch = Sample(_surfaces.Tracks, Blue)[0];
            Assert.Greater(bowl, TrampleStamps.GroundLevelBias + 0.05f, "no crater bowl was dug");
            Assert.Greater(scorch, 0.9f, "no scorch was drawn");

            Draw(new TrampleStamps(), TrampleBrush.Levels, TrampleBrush.Levels);

            // The bowl's lumpy floor puts its centre anywhere near the full depth, so compare it with itself.
            Assert.AreEqual(bowl, Sample(_surfaces.Tracks, Alpha)[0], 1.5f / 255f, "the crater bowl faded");
            Assert.AreEqual(scorch, Sample(_surfaces.Tracks, Blue)[0], 1.5f / 255f, "the scorch faded");
        }

        // The sample sits near the footprint's corner, outside the disc a crater scar would draw.
        [Test]
        public void FootprintScar_ScorchesToTheCorners_AndOutlastsTheFade()
        {
            var stamps = new TrampleStamps();
            stamps.AddFootprintScar(new float2(47f, 117f), new float2(0f, 1f), new float2(10f), 1f);
            Draw(stamps, 0, 0);

            Draw(new TrampleStamps(), TrampleBrush.Levels, TrampleBrush.Levels);

            var scorch = Sample(_surfaces.Tracks, Blue);
            Assert.Greater(scorch[0], 0.9f, "the corner is not scorched");
            Assert.Less(scorch[1], 0.01f);
        }

        // The first point lies just past one footprint's edge, the second past the other band's widest reach.
        [Test]
        public void WornGround_IsMudAtTheFootprint_AndGoneWellPastItsWidth()
        {
            var stamps = new TrampleStamps();
            stamps.AddWornGround(new TurnedRect(new float2(50f, 115f), new float2(1f, 0f), new float2(8f, 9.6f)), 3f, 1f, 7u);
            stamps.AddWornGround(new TurnedRect(new float2(50f, -112f), new float2(1f, 0f), new float2(8f, 6f)), 3f, 1f, 7u);
            Draw(stamps, 0, 0);

            var mud = Sample(_surfaces.Stains, Alpha);
            Assert.Greater(mud[0], 0.5f, "no mud at the footprint's edge");
            Assert.Less(mud[1], 0.01f, "the mud reaches far past its width");
        }

        [Test]
        public void Stains_KeepTheStrongestStamp_AndNeverFade()
        {
            var stamps = new TrampleStamps();
            stamps.AddStain(new float2(50f, 120f), 4f, new float4(0.8f, 0f, 0f, 0.3f));
            stamps.AddStain(new float2(50f, 120f), 4f, new float4(0.4f, 0f, 0f, 0.9f));
            Draw(stamps, 0, 0);

            Draw(new TrampleStamps(), TrampleBrush.Levels, TrampleBrush.Levels, TrampleBrush.Levels);

            Assert.AreEqual(0.8f, Sample(_surfaces.Stains, Red)[0], 0.02f, "a weaker stamp lowered the rubble");
            Assert.AreEqual(0.9f, Sample(_surfaces.Stains, Alpha)[0], 0.02f, "the mud faded or did not build up");
            Assert.Less(Sample(_surfaces.Stains, Red)[1], 0.01f);

            Draw(new TrampleStamps { Reset = true }, 0, 0);
            Assert.Less(Sample(_surfaces.Stains, Red)[0], 0.01f, "a replay seek kept the stains");
        }

        [Test]
        public void Wake_FoamsBehindTheUnit_ThenFades()
        {
            var stamps = new TrampleStamps();
            stamps.AddWake(new float2(50f, 121f), new float2(0f, 1f), TrampleStamps.FullWakeSpeed, 3f);
            Draw(stamps, 0, 0);
            Assert.Greater(Sample(_surfaces.Wake, Red)[0], 0.4f, "no foam behind the unit");

            Draw(new TrampleStamps(), 0, 0, TrampleBrush.Levels);
            Assert.Less(Sample(_surfaces.Wake, Red)[0], 0.01f, "the wake never faded");
        }

        private void DrawPass(TrampleStamp stamp)
        {
            var stamps = new TrampleStamps();
            stamps.Tracks.Add(stamp);
            Draw(stamps, 0, 0);
        }

        private void Draw(TrampleStamps stamps, int recovery, int fade, int wakeFade = 0)
        {
            _surfaces.Publish();
            _brush.Draw(_surfaces, recovery, fade, wakeFade, stamps);
            TrampleSurfaces.Clear();
        }

        private float[] Sample(Texture source, Vector4 channel)
        {
            using var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(float));
            var kernel = _probe.FindKernel("Probe");
            _probe.SetTexture(kernel, "_Source", source);
            _probe.SetBuffer(kernel, "_Samples", buffer);
            _probe.SetVector("_Rect", (Vector4)_surfaces.Rect);
            _probe.SetVector("_Points", (Vector4)Points);
            _probe.SetVector("_Channel", channel);
            _probe.Dispatch(kernel, 1, 1, 1);
            var samples = new float[2];
            buffer.GetData(samples);
            return samples;
        }
    }
}
