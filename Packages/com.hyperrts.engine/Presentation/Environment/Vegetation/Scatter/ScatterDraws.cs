using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>
    /// A scatter batch's indirect draws: one command and one grow counter per slot, as Scatter.hlsl reads them, and
    /// the queued jobs uploaded for the grow kernel.
    /// </summary>
    internal sealed class ScatterDraws : IDisposable
    {
        private static readonly int JobsId = Shader.PropertyToID("_Jobs");
        private static readonly int CountersId = Shader.PropertyToID("_Counters");
        private static readonly int ArgsId = Shader.PropertyToID("_Args");

        private readonly Mesh[] _meshes;
        private readonly GraphicsBuffer _args;
        private readonly GraphicsBuffer _counters;
        private readonly GrowingBuffer _jobs = new(GraphicsBuffer.Target.Structured, UnsafeUtility.SizeOf<ScatterJob>(), 64);

        /// <summary>Draws for <paramref name="meshes"/>, one slot each.</summary>
        public ScatterDraws(Mesh[] meshes)
        {
            _meshes = meshes;
            var count = Mathf.Max(1, meshes.Length);
            _counters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(uint));
            _counters.SetData(new uint[count]);
            _args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, count,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _args.SetData(Commands(count));
        }

        /// <summary>Uploads the queued jobs, growing the buffer when there are more, and binds them and the counters to the grow kernel.</summary>
        public void BindGrow(List<ScatterJob> jobs, ComputeShader compute, int kernel)
        {
            _jobs.Reserve(jobs.Count);
            _jobs.Buffer.SetData(jobs);
            compute.SetBuffer(kernel, JobsId, _jobs.Buffer);
            compute.SetBuffer(kernel, CountersId, _counters);
        }

        /// <summary>Binds the counters and draw commands to the kernel that turns counts into draws.</summary>
        public void BindCount(ComputeShader compute, int kernel)
        {
            compute.SetBuffer(kernel, CountersId, _counters);
            compute.SetBuffer(kernel, ArgsId, _args);
        }

        /// <summary>Draws one slot's mesh with the instance count the GPU wrote.</summary>
        public void Draw(in RenderParams parameters, int slot) =>
            Graphics.RenderMeshIndirect(parameters, _meshes[slot], _args, 1, slot);

        public void Dispose()
        {
            _jobs.Dispose();
            _args.Release();
            _counters.Release();
        }

        private GraphicsBuffer.IndirectDrawIndexedArgs[] Commands(int count)
        {
            var commands = new GraphicsBuffer.IndirectDrawIndexedArgs[count];
            for (var slot = 0; slot < _meshes.Length; slot++)
            {
                commands[slot] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = _meshes[slot].GetIndexCount(0),
                    startIndex = _meshes[slot].GetIndexStart(0),
                    baseVertexIndex = _meshes[slot].GetBaseVertex(0),
                };
            }

            return commands;
        }
    }
}
