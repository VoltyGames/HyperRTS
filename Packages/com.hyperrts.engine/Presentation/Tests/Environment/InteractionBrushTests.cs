using HyperRTS.Presentation.Environment;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment
{
    /// <summary>The brush drawn into real render targets and read back as the shaders read them.</summary>
    public class InteractionBrushTests
    {
        private const string ProbePath = "Packages/com.hyperrts.engine/Presentation/Tests/Environment/InteractionProbe.compute";

        private static readonly Vector4 Red = new(1f, 0f, 0f, 0f);
        private static readonly Vector4 Blue = new(0f, 0f, 1f, 0f);
        private static readonly Vector4 Alpha = new(0f, 0f, 0f, 1f);
        private static readonly float4 Points = new(50f, 120f, 50f, -120f);

        private ComputeShader _probe;
        private InteractionSurfaces _surfaces;
        private InteractionBrush _brush;
        private ImpactMark _mark;

        [SetUp]
        public void SetUp()
        {
            _probe = AssetDatabase.LoadAssetAtPath<ComputeShader>(ProbePath);
            _surfaces = new InteractionSurfaces(new float2(-200f, -200f), new float2(400f), InteractionQuality.Full);
            _brush = new InteractionBrush(Shader.Find(InteractionBrush.ShaderName));
            _brush.Clear(_surfaces);
            _mark = ScriptableObject.CreateInstance<ImpactMark>();
            _mark.craterDepth = 1f;
            _mark.scorch = 1f;
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
            var stamps = new InteractionStamps();
            stamps.Tracks.Add(new InteractionStamp
            {
                Center = new float2(50f, 120f), Forward = new float2(0f, 1f), HalfSize = new float2(3f), Value = new float4(1f, 1f, 0f, 0f),
            });
            Draw(stamps, 0, 0);

            var samples = Sample(_surfaces.Tracks, Red);

            Assert.Greater(samples[0], 0.5f, "no rut where the stamp was drawn");
            Assert.Less(samples[1], 0.01f, "the stamp shows up mirrored across the map");
        }

        [Test]
        public void Scars_OutlastTheFade_WhileRutsFade()
        {
            var stamps = new InteractionStamps();
            stamps.AddCrater(new float2(50f, 120f), _mark, 6f);
            Draw(stamps, 0, 0);

            Draw(new InteractionStamps(), InteractionMath.Levels, InteractionMath.Levels);

            Assert.Greater(Sample(_surfaces.Tracks, Alpha)[0], 0.4f, "the crater bowl faded");
            Assert.Greater(Sample(_surfaces.Tracks, Blue)[0], 0.9f, "the scorch faded");
        }

        [Test]
        public void Reset_ClearsTheScars()
        {
            var stamps = new InteractionStamps();
            stamps.AddCrater(new float2(50f, 120f), _mark, 6f);
            Draw(stamps, 0, 0);

            Draw(new InteractionStamps { Reset = true }, 0, 0);

            Assert.Less(Sample(_surfaces.Tracks, Alpha)[0], 0.01f);
            Assert.Less(Sample(_surfaces.Tracks, Blue)[0], 0.01f);
        }

        [Test]
        public void Wake_FoamsBehindTheUnit_ThenFades()
        {
            var stamps = new InteractionStamps();
            stamps.AddWake(new float2(50f, 121f), new float2(0f, 1f), InteractionMath.FullWakeSpeed, 3f);
            Draw(stamps, 0, 0);
            Assert.Greater(Sample(_surfaces.Wake, Red)[0], 0.4f, "no foam behind the unit");

            Draw(new InteractionStamps(), 0, 0, InteractionMath.Levels);
            Assert.Less(Sample(_surfaces.Wake, Red)[0], 0.01f, "the wake never faded");
        }

        private void Draw(InteractionStamps stamps, int recovery, int fade, int wakeFade = 0)
        {
            _surfaces.Publish();
            _brush.Draw(_surfaces, recovery, fade, wakeFade, stamps);
            InteractionSurfaces.Clear();
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
