using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// One camera's scatter on the GPU: a mesh and property block per slot, the jobs queued for the grow kernel, and
    /// the indirect draws the count kernel fills. Subclasses own the slots' room, buffers and look.
    /// </summary>
    internal abstract class ScatterBatch<TJob> : IDisposable where TJob : struct
    {
        // Matches GrassInstance.hlsl and RowCropInstance.hlsl.
        protected const int InstanceStride = 48;
        private const int ThreadGroup = 64;

        private readonly List<TJob> _pending = new();
        private readonly ScatterDraws _draws;
        private int _largestJob;

        /// <summary>Slots drawing <paramref name="meshes"/>, grown by <paramref name="grow"/> from jobs of <paramref name="jobStride"/> bytes.</summary>
        protected ScatterBatch(ComputeShader compute, int grow, int count, Mesh[] meshes, int jobStride)
        {
            Compute = compute;
            GrowKernel = grow;
            CountKernel = count;
            Meshes = meshes;
            Blocks = new MaterialPropertyBlock[meshes.Length];
            for (var slot = 0; slot < meshes.Length; slot++)
            {
                Blocks[slot] = new MaterialPropertyBlock();
            }

            _draws = new ScatterDraws(meshes, jobStride);
        }

        protected ComputeShader Compute { get; }

        protected int GrowKernel { get; }

        protected int CountKernel { get; }

        protected Mesh[] Meshes { get; }

        protected MaterialPropertyBlock[] Blocks { get; }

        /// <summary>Instances the last run drew and the slots that drew any, read back from the GPU for profiling.</summary>
        public (int Instances, int Draws) Drawn() => _draws.Drawn();

        /// <summary>Forgets the last view's tiles.</summary>
        public void Begin()
        {
            _pending.Clear();
            _largestJob = 0;
            ForgetView();
        }

        /// <summary>Grows the queued jobs into the slots. The renderer's compute inputs bind the shared inputs first.</summary>
        public void Generate()
        {
            Reserve();
            if (_pending.Count == 0)
            {
                return;
            }

            _draws.BindGrow(_pending, Compute, GrowKernel);
            Grow();
            BindCapacities();
            _draws.BindCount(Compute, CountKernel);
            Compute.Dispatch(CountKernel, 1, 1, 1);
        }

        /// <summary>Draws, for <paramref name="camera"/> only, each slot with room, culled by its tiles' box.</summary>
        public void Draw(int layer, Camera camera)
        {
            for (var slot = 0; slot < Meshes.Length; slot++)
            {
                if (!HasRoom(slot))
                {
                    continue;
                }

                var parameters = Parameters(slot);
                parameters.camera = camera;
                parameters.layer = layer;
                parameters.matProps = Blocks[slot];
                parameters.receiveShadows = true;
                parameters.motionVectorMode = MotionVectorGenerationMode.Object;
                _draws.Draw(parameters, slot);
            }
        }

        public void Dispose()
        {
            ReleaseBuffers();
            _draws.Dispose();
        }

        /// <summary>Queues one job, which runs <paramref name="threads"/> threads of the grow kernel.</summary>
        protected void Queue(TJob job, int threads)
        {
            _pending.Add(job);
            _largestJob = Mathf.Max(_largestJob, threads);
        }

        /// <summary>Runs the grow kernel over every queued job, one thread group row per job.</summary>
        protected void DispatchGrow()
        {
            var groups = Mathf.Max(1, (_largestJob + ThreadGroup - 1) / ThreadGroup);
            Compute.Dispatch(GrowKernel, groups, 1, _pending.Count);
        }

        /// <summary>Clears the room the last view's tiles asked for.</summary>
        protected abstract void ForgetView();

        /// <summary>Gives every slot room for the queued tiles before the grow kernel writes into it.</summary>
        protected abstract void Reserve();

        /// <summary>Binds the slots' buffers and runs the grow kernel.</summary>
        protected abstract void Grow();

        /// <summary>Binds each slot's capacity and the slot count to the count kernel.</summary>
        protected abstract void BindCapacities();

        protected abstract bool HasRoom(int slot);

        /// <summary>The slot's material, culling box and shadows.</summary>
        protected abstract RenderParams Parameters(int slot);

        protected abstract void ReleaseBuffers();
    }
}
