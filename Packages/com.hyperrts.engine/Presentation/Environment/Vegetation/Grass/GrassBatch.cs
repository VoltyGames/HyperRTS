using System;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Vegetation.Grass
{
    /// <summary>
    /// One camera's grass on the GPU: the visible tiles, a slot per (plant, model, band) that Grass.compute fills, and
    /// the indirect draws.
    /// </summary>
    public sealed class GrassBatch : ScatterBatch
    {
        private const int MinInstances = 4096;

        private static readonly int HealthyId = Shader.PropertyToID("_GrassHealthy");
        private static readonly int DryId = Shader.PropertyToID("_GrassDry");

        private readonly GrassField _field;
        private readonly Material[] _materials;
        private readonly int[] _firstSlots;
        private readonly int[] _slotPlants;
        private readonly float[] _expected;
        private readonly uint[] _capacities;
        private readonly Bounds[] _bounds;

        /// <summary>A batch drawing <paramref name="field"/>'s plants with their instanced materials, in plant order.</summary>
        internal GrassBatch(GrassField field, GrassComputeInputs kernels, Material[] materials)
            : this(field, kernels, materials, field.FirstSlots())
        {
        }

        private GrassBatch(GrassField field, GrassComputeInputs kernels, Material[] materials, int[] firstSlots)
            : base(kernels, SlotMeshes(field, firstSlots), MinInstances)
        {
            _field = field;
            _materials = materials;
            _firstSlots = firstSlots;
            var count = Meshes.Length;
            _slotPlants = SlotPlants(field, _firstSlots);
            _expected = new float[count];
            _capacities = new uint[count];
            _bounds = new Bounds[field.plants.Count * GrassBands.Bands];
            for (var slot = 0; slot < count; slot++)
            {
                var plant = field.plants[_slotPlants[slot]];
                Blocks[slot].SetColor(HealthyId, plant.healthy);
                Blocks[slot].SetColor(DryId, plant.dry);
            }
        }

        /// <summary>Whether the batch no longer draws the field's plants as they are: another plant count or other models.</summary>
        internal bool Stale()
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
                    for (var band = 0; band < GrassBands.Bands; band++, slot++)
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

        private protected override void ForgetView() => Array.Clear(_expected, 0, _expected.Length);

        /// <summary>Queues one plant of a visible tile, <paramref name="near"/> to <paramref name="far"/> metres away.</summary>
        internal void Add(ScatterJob job, float coverage, Bounds bounds, (float Near, float Far) range, ScatterSettings settings)
        {
            Queue(job);
            var plant = _field.plants[job.id];
            var share = job.Threads * coverage * settings.Density * GrassBands.Drawn(range.Near, settings.Distance) /
                        plant.meshes.Count;
            var first = _firstSlots[job.id];
            for (var band = 0; band < GrassBands.Bands; band++)
            {
                if (!GrassBands.InBand(band, settings.ShadowDistance, range.Near, range.Far))
                {
                    continue;
                }

                var box = job.id * GrassBands.Bands + band;
                if (_expected[first + band] == 0f)
                {
                    _bounds[box] = bounds;
                }

                _bounds[box].Encapsulate(bounds);
                for (var variant = 0; variant < plant.meshes.Count; variant++)
                {
                    _expected[first + variant * GrassBands.Bands + band] += share;
                }
            }
        }

        private protected override RenderParams Parameters(int slot)
        {
            var band = slot % GrassBands.Bands;
            var plant = _slotPlants[slot];
            return new RenderParams(_materials[plant])
            {
                worldBounds = _bounds[plant * GrassBands.Bands + band],
                shadowCastingMode = band == GrassBands.Near ? ShadowCastingMode.On : ShadowCastingMode.Off,
            };
        }

        // Lays the slots out end to end in the one instance buffer.
        private protected override void Reserve()
        {
            for (var slot = 0; slot < _expected.Length; slot++)
            {
                _capacities[slot] = _expected[slot] > 0f ? (uint)GrassBands.Capacity(_expected[slot]) : 0u;
            }

            Slots.Fit(_capacities);
        }

        // Slot = the plant's first slot + model * Bands + band, the order of the table, counters and draw commands.
        private static Mesh[] SlotMeshes(GrassField field, int[] firstSlots)
        {
            var meshes = new Mesh[firstSlots[field.plants.Count]];
            for (var p = 0; p < field.plants.Count; p++)
            {
                var models = field.plants[p].meshes;
                for (var slot = firstSlots[p]; slot < firstSlots[p + 1]; slot++)
                {
                    meshes[slot] = models[(slot - firstSlots[p]) / GrassBands.Bands];
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
