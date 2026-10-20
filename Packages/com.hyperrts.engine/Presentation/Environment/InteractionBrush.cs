using System;
using System.Collections.Generic;
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
        private const int ScarsPass = 3;
        private const int WakePass = 4;
        private const int FadePass = 5;

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

        public void Clear(InteractionSurfaces surfaces)
        {
            _commands.Clear();
            AppendClear(surfaces);
            Graphics.ExecuteCommandBuffer(_commands);
        }

        public void Draw(InteractionSurfaces surfaces, int recoverySteps, int fadeSteps, int wakeSteps, InteractionStamps stamps)
        {
            Upload(stamps);
            _commands.Clear();
            if (stamps.Reset)
            {
                AppendClear(surfaces);
            }

            _commands.SetGlobalBuffer(StampsId, _buffer);
            var offset = 0;
            _commands.SetRenderTarget(surfaces.Trample);
            Fade(new Vector4(0f, 0f, recoverySteps, 0f) / InteractionMath.Levels);
            DrawStamps(TrampleDirectionPass, offset, stamps.Trample.Count);
            DrawStamps(TrampleAmountPass, offset, stamps.Trample.Count);
            offset += stamps.Trample.Count;

            // Scars (BA) never fade: they last the match.
            _commands.SetRenderTarget(surfaces.Tracks);
            Fade(new Vector4(fadeSteps, fadeSteps, 0f, 0f) / InteractionMath.Levels);
            DrawStamps(TracksPass, offset, stamps.Tracks.Count);
            offset += stamps.Tracks.Count;
            DrawStamps(ScarsPass, offset, stamps.Scars.Count);
            offset += stamps.Scars.Count;

            _commands.SetRenderTarget(surfaces.Wake);
            Fade(new Vector4(wakeSteps, 0f, 0f, 0f) / InteractionMath.Levels);
            DrawStamps(WakePass, offset, stamps.Wakes.Count);
            Graphics.ExecuteCommandBuffer(_commands);
        }

        public void Dispose()
        {
            _commands.Release();
            _buffer?.Release();
            CoreUtils.Destroy(_material);
        }

        // One buffer in Draw's order, so each pass draws a slice of it.
        private void Upload(InteractionStamps stamps)
        {
            var needed = Mathf.Max(stamps.Trample.Count + stamps.Tracks.Count + stamps.Scars.Count + stamps.Wakes.Count, 1);
            if (_buffer == null || _buffer.count < needed)
            {
                _buffer?.Release();
                _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.NextPowerOfTwo(Mathf.Max(needed, 256)),
                    InteractionStamp.Stride);
            }

            var offset = 0;
            Put(stamps.Trample, ref offset);
            Put(stamps.Tracks, ref offset);
            Put(stamps.Scars, ref offset);
            Put(stamps.Wakes, ref offset);
        }

        private void Put(List<InteractionStamp> list, ref int offset)
        {
            _buffer.SetData(list, 0, offset, list.Count);
            offset += list.Count;
        }

        private void AppendClear(InteractionSurfaces surfaces)
        {
            _commands.SetRenderTarget(surfaces.Trample);
            _commands.ClearRenderTarget(false, true, InteractionSurfaces.TrampleClear);
            _commands.SetRenderTarget(surfaces.Tracks);
            _commands.ClearRenderTarget(false, true, Color.clear);
            _commands.SetRenderTarget(surfaces.Wake);
            _commands.ClearRenderTarget(false, true, Color.clear);
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
