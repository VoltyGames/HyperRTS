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

        private static readonly int SlotsId = Shader.PropertyToID("_Slots");
        private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int GrassInstancesId = Shader.PropertyToID("_GrassInstances");
        private static readonly int GrassOffsetId = Shader.PropertyToID("_GrassOffset");
        private static readonly int HealthyId = Shader.PropertyToID("_GrassHealthy");
        private static readonly int DryId = Shader.PropertyToID("_GrassDry");

        private readonly GrassField _field;
        private readonly GrassComputeInputs _kernels;
        private readonly Material[] _materials;
        private readonly Mesh[] _meshes;
        private readonly int[] _firstSlots;
        private readonly int[] _plantOf;
        private readonly float[] _expected;
        private readonly uint[] _table;
        private readonly Bounds[] _bounds;
        private readonly MaterialPropertyBlock[] _blocks;
        private readonly List<GrassJob> _pending = new();
        private readonly ScatterDraws _draws;
        private readonly GraphicsBuffer _slots;
        private GraphicsBuffer _instances;
        private int _maxCells;

        /// <summary>A batch drawing <paramref name="field"/>'s plants with their instanced materials, in plant order.</summary>
        public GrassBatch(GrassField field, GrassComputeInputs kernels, Material[] materials)
        {
            _field = field;
            _kernels = kernels;
            _materials = materials;
            _firstSlots = field.FirstSlots();
            var count = _firstSlots[field.plants.Count];
            (_meshes, _plantOf) = Slots(count);
            _expected = new float[count];
            _table = new uint[count * 2];
            _bounds = new Bounds[field.plants.Count * GrassView.Bands];
            _blocks = new MaterialPropertyBlock[count];
            _draws = new ScatterDraws(_meshes, GrassJob.Stride);
            _slots = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, count), sizeof(uint) * 2);
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
        public (int Instances, int Draws) Drawn() => _draws.Drawn();

        /// <summary>Whether the batch no longer draws the field's plants as they are: other models or materials.</summary>
        public bool Stale()
        {
            if (_materials.Length != _field.plants.Count)
            {
                return true;
            }

            // Walks the field's models in slot order without building a new slot table each camera render.
            var slot = 0;
            foreach (var plant in _field.plants)
            {
                foreach (var mesh in plant.meshes)
                {
                    for (var band = 0; band < GrassView.Bands; band++, slot++)
                    {
                        if (slot >= _meshes.Length || _meshes[slot] != mesh)
                        {
                            return true;
                        }
                    }
                }
            }

            return slot != _meshes.Length;
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
            var first = _firstSlots[job.plant];
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

            var compute = _kernels.Compute;
            _draws.BindGrow(_pending, compute, _kernels.Scatter);
            compute.SetBuffer(_kernels.Scatter, SlotsId, _slots);
            compute.SetBuffer(_kernels.Scatter, InstancesId, _instances);
            var groups = Mathf.Max(1, (_maxCells + ThreadGroup - 1) / ThreadGroup);
            compute.Dispatch(_kernels.Scatter, groups, 1, _pending.Count);
            compute.SetBuffer(_kernels.Count, SlotsId, _slots);
            _draws.BindCount(compute, _kernels.Count);
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
                _draws.Draw(parameters, slot);
            }
        }

        public void Dispose()
        {
            _instances.Release();
            _draws.Dispose();
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

        // Slot = the plant's first slot + model * Bands + band, the order of the table, counters and draw commands.
        private (Mesh[] Meshes, int[] Plants) Slots(int count)
        {
            var meshes = new Mesh[count];
            var plants = new int[count];
            for (var p = 0; p < _field.plants.Count; p++)
            {
                var models = _field.plants[p].meshes;
                for (var slot = _firstSlots[p]; slot < _firstSlots[p + 1]; slot++)
                {
                    meshes[slot] = models[(slot - _firstSlots[p]) / GrassView.Bands];
                    plants[slot] = p;
                }
            }

            return (meshes, plants);
        }
    }
}
