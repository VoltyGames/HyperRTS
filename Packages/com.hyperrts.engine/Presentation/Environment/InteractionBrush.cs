using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>Fades the interaction maps and draws a frame of stamps into them as instanced quads.</summary>
    public sealed class InteractionBrush : IDisposable
    {
        public const string ShaderName = "Hidden/HyperRTS/Interaction Brush";

        private const int TrampleDirectionPass = 0;
        private const int TrampleAmountPass = 1;
        private const int TracksPass = 2;
        private const int FadePass = 3;

        private static readonly int StampsId = Shader.PropertyToID("_InteractionStamps");
        private static readonly int OffsetId = Shader.PropertyToID("_InteractionStampOffset");
        private static readonly int FadeId = Shader.PropertyToID("_InteractionFade");

        private readonly Material _material;
        private readonly CommandBuffer _commands = new() { name = "HyperRTS Interaction Maps" };
        private GraphicsBuffer _buffer;

        public InteractionBrush(Shader shader)
        {
            if (shader == null || shader.name != ShaderName)
            {
                throw new ArgumentException($"HyperRTS: the interaction brush needs the '{ShaderName}' shader.");
            }

            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
        }

        /// <summary>Clears both maps to no direction, standing plants and clean ground.</summary>
        public void Clear(InteractionSurfaces surfaces)
        {
            _commands.Clear();
            _commands.SetRenderTarget(surfaces.Trample);
            _commands.ClearRenderTarget(false, true, InteractionSurfaces.TrampleClear);
            _commands.SetRenderTarget(surfaces.Tracks);
            _commands.ClearRenderTarget(false, true, Color.clear);
            Graphics.ExecuteCommandBuffer(_commands);
        }

        public void Draw(InteractionSurfaces surfaces, int recoverySteps, int fadeSteps, InteractionStamps stamps)
        {
            Upload(stamps);
            _commands.Clear();
            _commands.SetGlobalBuffer(StampsId, _buffer);

            _commands.SetRenderTarget(surfaces.Trample);
            Fade(new Vector4(0f, 0f, recoverySteps, 0f) / InteractionMath.Levels);
            DrawStamps(TrampleDirectionPass, 0, stamps.Trample.Count);
            DrawStamps(TrampleAmountPass, 0, stamps.Trample.Count);

            _commands.SetRenderTarget(surfaces.Tracks);
            Fade(new Vector4(fadeSteps, fadeSteps, 0f, 0f) / InteractionMath.Levels);
            DrawStamps(TracksPass, stamps.Trample.Count, stamps.Tracks.Count);
            Graphics.ExecuteCommandBuffer(_commands);
        }

        public void Dispose()
        {
            _commands.Release();
            _buffer?.Release();
            CoreUtils.Destroy(_material);
        }

        // One buffer: tracks start after the trample stamps.
        private void Upload(InteractionStamps stamps)
        {
            var trample = stamps.Trample.Count;
            var needed = Mathf.Max(trample + stamps.Tracks.Count, 1);
            if (_buffer == null || _buffer.count < needed)
            {
                _buffer?.Release();
                _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.NextPowerOfTwo(Mathf.Max(needed, 256)),
                    InteractionStamp.Stride);
            }

            _buffer.SetData(stamps.Trample, 0, 0, trample);
            _buffer.SetData(stamps.Tracks, 0, trample, stamps.Tracks.Count);
        }

        private void Fade(Vector4 amount)
        {
            if (amount == Vector4.zero)
            {
                return;
            }

            _commands.SetGlobalVector(FadeId, amount);
            _commands.DrawProcedural(Matrix4x4.identity, _material, FadePass, MeshTopology.Triangles, 3);
        }

        private void DrawStamps(int pass, int offset, int count)
        {
            if (count == 0)
            {
                return;
            }

            _commands.SetGlobalInt(OffsetId, offset);
            _commands.DrawProcedural(Matrix4x4.identity, _material, pass, MeshTopology.Triangles, 6, count);
        }
    }
}
