using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// A scatter batch's room: one instance buffer cut into a slot per draw, and the table of each slot's offset and
    /// capacity that the grow and count kernels read (the layout Scatter.hlsl declares).
    /// </summary>
    internal sealed class ScatterSlots : IDisposable
    {
        // Matches GrassInstance.hlsl and RowCropInstance.hlsl.
        public const int InstanceStride = 48;

        private static readonly int SlotsId = Shader.PropertyToID("_Slots");
        private static readonly int SlotCountId = Shader.PropertyToID("_SlotCount");
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int SlotOffsetId = Shader.PropertyToID("_SlotOffset");

        private readonly MaterialPropertyBlock[] _blocks;
        private readonly int _minInstances;
        private readonly uint[] _table;
        private readonly GraphicsBuffer _slots;
        private GraphicsBuffer _instances;

        /// <summary>Room for <paramref name="blocks"/>' slots, each drawn from its own block.</summary>
        public ScatterSlots(MaterialPropertyBlock[] blocks, int minInstances)
        {
            _blocks = blocks;
            _minInstances = minInstances;
            _table = new uint[blocks.Length * 2];
            _slots = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, blocks.Length), sizeof(uint) * 2);
            _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, minInstances, InstanceStride);
            BindDraws();
        }

        /// <summary>Whether the slot has room for any instance.</summary>
        public bool HasRoom(int slot) => _table[slot * 2 + 1] != 0u;

        /// <summary>Lays the slots out end to end with these capacities, growing the buffer when they need more room.</summary>
        public void Fit(uint[] capacities)
        {
            var total = 0u;
            for (var slot = 0; slot < capacities.Length; slot++)
            {
                _table[slot * 2] = total;
                _table[slot * 2 + 1] = capacities[slot];
                total += capacities[slot];
            }

            if (total > _instances.count)
            {
                _instances.Release();
                _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    Mathf.Max(_minInstances, (int)(total + total / 4)), InstanceStride);
            }

            BindDraws();
            _slots.SetData(_table);
        }

        /// <summary>Binds the table and the instance buffer to the grow kernel.</summary>
        public void BindGrow(ComputeShader compute, int kernel)
        {
            compute.SetBuffer(kernel, SlotsId, _slots);
            compute.SetBuffer(kernel, InstancesId, _instances);
        }

        /// <summary>Binds the table and slot count to the kernel that turns counts into draws.</summary>
        public void BindCount(ComputeShader compute, int kernel)
        {
            compute.SetBuffer(kernel, SlotsId, _slots);
            compute.SetInt(SlotCountId, _blocks.Length);
        }

        public void Dispose()
        {
            _instances.Release();
            _slots.Release();
        }

        // Each draw reads the one buffer from its slot's offset.
        private void BindDraws()
        {
            for (var slot = 0; slot < _blocks.Length; slot++)
            {
                _blocks[slot].SetBuffer(InstancesId, _instances);
                _blocks[slot].SetInteger(SlotOffsetId, (int)_table[slot * 2]);
            }
        }
    }
}
