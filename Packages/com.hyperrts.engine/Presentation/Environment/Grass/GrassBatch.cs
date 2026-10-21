using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>
    /// One camera's grass on the GPU: the visible tiles, one instance buffer cut into a slot per (plant, model, band)
    /// that Grass.compute fills, and the indirect draws.
    /// </summary>
    internal sealed class GrassBatch : IDisposable
    {
        // Matches GrassInstance.hlsl.
        private const int InstanceStride = 48;
        private const int ThreadGroup = 64;
        private const int MinInstances = 4096;

        private static readonly int JobsId = Shader.PropertyToID("_Jobs");
        private static readonly int SlotsId = Shader.PropertyToID("_Slots");
        private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");
        private static readonly int CountersId = Shader.PropertyToID("_Counters");
        private static readonly int ArgsId = Shader.PropertyToID("_Args");
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int GrassInstancesId = Shader.PropertyToID("_GrassInstances");
        private static readonly int GrassOffsetId = Shader.PropertyToID("_GrassOffset");
        private static readonly int HealthyId = Shader.PropertyToID("_GrassHealthy");
        private static readonly int DryId = Shader.PropertyToID("_GrassDry");

        private readonly GrassField _field;
        private readonly GrassComputeInputs _kernels;
        private readonly Material[] _materials;
        private readonly Mesh[] _meshes;
        private readonly int[] _plantOf;
        private readonly float[] _expected;
        private readonly uint[] _table;
        private readonly Bounds[] _bounds;
        private readonly MaterialPropertyBlock[] _blocks;
        private readonly List<GrassJob> _pending = new();
        private readonly GraphicsBuffer _args;
        private readonly GraphicsBuffer _counters;
        private readonly GraphicsBuffer _slots;
        private GraphicsBuffer _instances;
        private GraphicsBuffer _jobs;
        private int _maxCells;

        /// <summary>A batch drawing <paramref name="field"/>'s plants with their instanced materials, in plant order.</summary>
        public GrassBatch(GrassField field, GrassComputeInputs kernels, Material[] materials)
        {
            _field = field;
            _kernels = kernels;
            _materials = materials;
            var count = field.SlotCount;
            (_meshes, _plantOf) = Slots(field, count);
            _expected = new float[count];
            _table = new uint[count * 2];
            _bounds = new Bounds[field.plants.Count * GrassView.Bands];
            _blocks = new MaterialPropertyBlock[count];
            _counters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, count), sizeof(uint));
            _counters.SetData(new uint[Mathf.Max(1, count)]);
            _slots = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, count), sizeof(uint) * 2);
            _args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, Mathf.Max(1, count),
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _args.SetData(Commands(_meshes));
            _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MinInstances, InstanceStride);
            for (var slot = 0; slot < count; slot++)
            {
                var plant = field.plants[_plantOf[slot]];
                _blocks[slot] = new MaterialPropertyBlock();
                _blocks[slot].SetColor(HealthyId, plant.healthy);
                _blocks[slot].SetColor(DryId, plant.dry);
                _blocks[slot].SetBuffer(GrassInstancesId, _instances);
            }
        }

        /// <summary>Plants the last run drew and the slots that drew any, read back from the GPU for profiling.</summary>
        public (int Instances, int Draws) Drawn()
        {
            var commands = new GraphicsBuffer.IndirectDrawIndexedArgs[_meshes.Length];
            _args.GetData(commands);
            var (instances, draws) = (0, 0);
            foreach (var command in commands)
            {
                instances += (int)command.instanceCount;
                draws += command.instanceCount > 0 ? 1 : 0;
            }

            return (instances, draws);
        }

        /// <summary>Whether the batch no longer draws the field's plants as they are: other models or materials.</summary>
        public bool Stale()
        {
            var (meshes, _) = Slots(_field, _field.SlotCount);
            if (meshes.Length != _meshes.Length || _materials.Length != _field.plants.Count)
            {
                return true;
            }

            for (var slot = 0; slot < meshes.Length; slot++)
            {
                if (meshes[slot] != _meshes[slot])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Forgets the last view's tiles.</summary>
        public void Begin()
        {
            _pending.Clear();
            _maxCells = 0;
            Array.Clear(_expected, 0, _expected.Length);
        }

        /// <summary>Queues one plant of a visible tile, <paramref name="near"/> to <paramref name="far"/> metres away.</summary>
        public void Add(GrassJob job, float coverage, Bounds bounds, (float Near, float Far) range, GrassSettings settings)
        {
            _pending.Add(job);
            _maxCells = Mathf.Max(_maxCells, job.Cells);
            var plant = _field.plants[job.plant];
            var share = job.Cells * coverage * settings.Density * GrassView.Drawn(range.Near, settings.Distance) /
                        plant.meshes.Count;
            var first = FirstSlot(job.plant);
            for (var band = 0; band < GrassView.Bands; band++)
            {
                if (!GrassView.InBand(band, settings.ShadowDistance, range.Near, range.Far))
                {
                    continue;
                }

                var box = job.plant * GrassView.Bands + band;
                if (_expected[first + band] == 0f)
                {
                    _bounds[box] = bounds;
                }

                _bounds[box].Encapsulate(bounds);
                for (var variant = 0; variant < plant.meshes.Count; variant++)
                {
                    _expected[first + variant * GrassView.Bands + band] += share;
                }
            }
        }

        /// <summary>Scatters the queued tiles into the slots. GrassComputeInputs binds the shared inputs first.</summary>
        public void Generate()
        {
            Reserve();
            if (_pending.Count == 0)
            {
                return;
            }

            UploadJobs();
            var compute = _kernels.Compute;
            compute.SetBuffer(_kernels.Scatter, SlotsId, _slots);
            compute.SetBuffer(_kernels.Scatter, CountersId, _counters);
            compute.SetBuffer(_kernels.Scatter, InstancesId, _instances);
            var groups = Mathf.Max(1, (_maxCells + ThreadGroup - 1) / ThreadGroup);
            compute.Dispatch(_kernels.Scatter, groups, 1, _pending.Count);
            compute.SetBuffer(_kernels.Count, SlotsId, _slots);
            compute.SetBuffer(_kernels.Count, CountersId, _counters);
            compute.SetBuffer(_kernels.Count, ArgsId, _args);
            compute.SetInt(SlotCountId, _meshes.Length);
            compute.Dispatch(_kernels.Count, 1, 1, 1);
        }

        /// <summary>Draws, for <paramref name="camera"/> only, each slot with room, culled by its tiles' box.</summary>
        public void Draw(int layer, Camera camera)
        {
            for (var slot = 0; slot < _meshes.Length; slot++)
            {
                if (_table[slot * 2 + 1] == 0u)
                {
                    continue;
                }

                var band = slot % GrassView.Bands;
                var plant = _plantOf[slot];
                var parameters = new RenderParams(_materials[plant])
                {
                    camera = camera,
                    worldBounds = _bounds[plant * GrassView.Bands + band],
                    layer = layer,
                    matProps = _blocks[slot],
                    receiveShadows = true,
                    shadowCastingMode = band == GrassView.Near ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    motionVectorMode = MotionVectorGenerationMode.Object,
                };
                Graphics.RenderMeshIndirect(parameters, _meshes[slot], _args, 1, slot);
            }
        }

        public void Dispose()
        {
            _instances.Release();
            _jobs?.Release();
            _args.Release();
            _counters.Release();
            _slots.Release();
        }

        // Lays the slots out end to end in the one instance buffer, growing it when the view needs more room.
        private void Reserve()
        {
            var total = 0u;
            for (var slot = 0; slot < _expected.Length; slot++)
            {
                var capacity = _expected[slot] > 0f ? (uint)GrassView.Capacity(_expected[slot]) : 0u;
                _table[slot * 2] = total;
                _table[slot * 2 + 1] = capacity;
                total += capacity;
            }

            if (total > _instances.count)
            {
                _instances.Release();
                _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)(total + total / 4), InstanceStride);
                Array.ForEach(_blocks, block => block.SetBuffer(GrassInstancesId, _instances));
            }

            for (var slot = 0; slot < _blocks.Length; slot++)
            {
                _blocks[slot].SetInteger(GrassOffsetId, (int)_table[slot * 2]);
            }

            _slots.SetData(_table);
        }

        private void UploadJobs()
        {
            if (_jobs == null || _jobs.count < _pending.Count)
            {
                _jobs?.Release();
                _jobs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(64, _pending.Count * 2), GrassJob.Stride);
            }

            _jobs.SetData(_pending);
            _kernels.Compute.SetBuffer(_kernels.Scatter, JobsId, _jobs);
        }

        private int FirstSlot(int plant)
        {
            var slot = 0;
            for (var p = 0; p < plant; p++)
            {
                slot += _field.plants[p].meshes.Count * GrassView.Bands;
            }

            return slot;
        }

        // Slot = the plant's first slot + model * Bands + band, the order of the table, counters and draw commands.
        private static (Mesh[] Meshes, int[] Plants) Slots(GrassField field, int count)
        {
            var meshes = new Mesh[count];
            var plants = new int[count];
            var slot = 0;
            for (var p = 0; p < field.plants.Count; p++)
            {
                foreach (var mesh in field.plants[p].meshes)
                {
                    for (var band = 0; band < GrassView.Bands; band++, slot++)
                    {
                        meshes[slot] = mesh;
                        plants[slot] = p;
                    }
                }
            }

            return (meshes, plants);
        }

        private static GraphicsBuffer.IndirectDrawIndexedArgs[] Commands(Mesh[] meshes)
        {
            var commands = new GraphicsBuffer.IndirectDrawIndexedArgs[Mathf.Max(1, meshes.Length)];
            for (var slot = 0; slot < meshes.Length; slot++)
            {
                commands[slot] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = meshes[slot].GetIndexCount(0),
                    startIndex = meshes[slot].GetIndexStart(0),
                    baseVertexIndex = meshes[slot].GetBaseVertex(0),
                };
            }

            return commands;
        }
    }
}
