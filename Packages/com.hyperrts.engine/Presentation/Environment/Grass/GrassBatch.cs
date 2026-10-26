using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>
    /// One camera's grass on the GPU: the visible tiles, one instance buffer cut into a slot per (plant, model, band)
    /// that Grass.compute fills, and the indirect draws.
    /// </summary>
    internal sealed class GrassBatch : ScatterBatch<GrassJob>
    {
        private const int MinInstances = 4096;

        private static readonly int SlotsId = Shader.PropertyToID("_Slots");
        private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int GrassInstancesId = Shader.PropertyToID("_GrassInstances");
        private static readonly int GrassOffsetId = Shader.PropertyToID("_GrassOffset");
        private static readonly int HealthyId = Shader.PropertyToID("_GrassHealthy");
        private static readonly int DryId = Shader.PropertyToID("_GrassDry");

        private readonly GrassField _field;
        private readonly Material[] _materials;
        private readonly int[] _firstSlots;
        private readonly int[] _slotPlants;
        private readonly float[] _expected;
        private readonly uint[] _table;
        private readonly Bounds[] _bounds;
        private readonly GraphicsBuffer _slots;
        private GraphicsBuffer _instances;

        /// <summary>A batch drawing <paramref name="field"/>'s plants with their instanced materials, in plant order.</summary>
        public GrassBatch(GrassField field, GrassComputeInputs kernels, Material[] materials)
            : base(kernels.Compute, kernels.Scatter, kernels.Count, SlotMeshes(field), GrassJob.Stride)
        {
            _field = field;
            _materials = materials;
            _firstSlots = field.FirstSlots();
            var count = Meshes.Length;
            _slotPlants = SlotPlants(field, _firstSlots);
            _expected = new float[count];
            _table = new uint[count * 2];
            _bounds = new Bounds[field.plants.Count * GrassView.Bands];
            _slots = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, count), sizeof(uint) * 2);
            _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MinInstances, InstanceStride);
            for (var slot = 0; slot < count; slot++)
            {
                var plant = field.plants[_slotPlants[slot]];
                Blocks[slot].SetColor(HealthyId, plant.healthy);
                Blocks[slot].SetColor(DryId, plant.dry);
                Blocks[slot].SetBuffer(GrassInstancesId, _instances);
            }
        }

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
                        if (slot >= Meshes.Length || Meshes[slot] != mesh)
                        {
                            return true;
                        }
                    }
                }
            }

            return slot != Meshes.Length;
        }

        protected override void ForgetView() => Array.Clear(_expected, 0, _expected.Length);

        /// <summary>Queues one plant of a visible tile, <paramref name="near"/> to <paramref name="far"/> metres away.</summary>
        public void Add(GrassJob job, float coverage, Bounds bounds, (float Near, float Far) range, GrassSettings settings)
        {
            Queue(job, job.Cells);
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

        protected override void Grow()
        {
            Compute.SetBuffer(GrowKernel, SlotsId, _slots);
            Compute.SetBuffer(GrowKernel, InstancesId, _instances);
            DispatchGrow();
        }

        protected override void BindCapacities()
        {
            Compute.SetBuffer(CountKernel, SlotsId, _slots);
            Compute.SetInt(SlotCountId, Meshes.Length);
        }

        protected override bool HasRoom(int slot) => _table[slot * 2 + 1] != 0u;

        protected override RenderParams Parameters(int slot)
        {
            var band = slot % GrassView.Bands;
            var plant = _slotPlants[slot];
            return new RenderParams(_materials[plant])
            {
                worldBounds = _bounds[plant * GrassView.Bands + band],
                shadowCastingMode = band == GrassView.Near ? ShadowCastingMode.On : ShadowCastingMode.Off,
            };
        }

        protected override void ReleaseBuffers()
        {
            _instances.Release();
            _slots.Release();
        }

        // Lays the slots out end to end in the one instance buffer, growing it when the view needs more room.
        protected override void Reserve()
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
                Array.ForEach(Blocks, block => block.SetBuffer(GrassInstancesId, _instances));
            }

            for (var slot = 0; slot < Blocks.Length; slot++)
            {
                Blocks[slot].SetInteger(GrassOffsetId, (int)_table[slot * 2]);
            }

            _slots.SetData(_table);
        }

        // Slot = the plant's first slot + model * Bands + band, the order of the table, counters and draw commands.
        private static Mesh[] SlotMeshes(GrassField field)
        {
            var firstSlots = field.FirstSlots();
            var meshes = new Mesh[firstSlots[field.plants.Count]];
            for (var p = 0; p < field.plants.Count; p++)
            {
                var models = field.plants[p].meshes;
                for (var slot = firstSlots[p]; slot < firstSlots[p + 1]; slot++)
                {
                    meshes[slot] = models[(slot - firstSlots[p]) / GrassView.Bands];
                }
            }

            return meshes;
        }

        private static int[] SlotPlants(GrassField field, int[] firstSlots)
        {
            var plants = new int[firstSlots[field.plants.Count]];
            for (var p = 0; p < field.plants.Count; p++)
            {
                Array.Fill(plants, p, firstSlots[p], firstSlots[p + 1] - firstSlots[p]);
            }

            return plants;
        }
    }
}
