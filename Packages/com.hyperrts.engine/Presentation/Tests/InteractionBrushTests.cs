using HyperRTS.Presentation.Environment;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public class InteractionBrushTests
    {
        private const string ProbePath = "Packages/com.hyperrts.engine/Presentation/Tests/InteractionProbe.compute";

        // Render targets flip rows on some graphics APIs: a stamp must read back where it was drawn, not mirrored.
        [Test]
        public void Stamp_IsSampledWhereItWasDrawn()
        {
            var probe = AssetDatabase.LoadAssetAtPath<ComputeShader>(ProbePath);
            using var surfaces = new InteractionSurfaces(new float2(-200f, -200f), new float2(400f), InteractionQuality.Full);
            using var brush = new InteractionBrush(Shader.Find(InteractionBrush.ShaderName));
            brush.Clear(surfaces);
            var stamps = new InteractionStamps();
            stamps.Tracks.Add(new InteractionStamp
            {
                Center = new float2(50f, 120f), Forward = new float2(0f, 1f), HalfSize = new float2(3f), Value = new float4(1f, 1f, 0f, 0f),
            });
            brush.Draw(surfaces, 0, 0, stamps);

            var samples = Sample(probe, surfaces, new float4(50f, 120f, 50f, -120f));

            Assert.Greater(samples[0], 0.5f, "no rut where the stamp was drawn");
            Assert.Less(samples[1], 0.01f, "the stamp shows up mirrored across the map");
        }

        private static float[] Sample(ComputeShader probe, InteractionSurfaces surfaces, float4 points)
        {
            using var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(float));
            var kernel = probe.FindKernel("Probe");
            probe.SetTexture(kernel, "_Source", surfaces.Tracks);
            probe.SetBuffer(kernel, "_Samples", buffer);
            probe.SetVector("_Rect", (Vector4)surfaces.Rect);
            probe.SetVector("_Points", (Vector4)points);
            probe.Dispatch(kernel, 1, 1, 1);
            var samples = new float[2];
            buffer.GetData(samples);
            return samples;
        }
    }
}
