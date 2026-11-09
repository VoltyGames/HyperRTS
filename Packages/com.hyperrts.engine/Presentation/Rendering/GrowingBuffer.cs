using System;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>A GPU buffer that is replaced by a larger one when more room is needed; contents are not kept.</summary>
    internal sealed class GrowingBuffer : IDisposable
    {
        private readonly GraphicsBuffer.Target _target;
        private readonly int _stride;

        public GrowingBuffer(GraphicsBuffer.Target target, int stride, int capacity)
        {
            _target = target;
            _stride = stride;
            Buffer = new GraphicsBuffer(target, Mathf.Max(1, capacity), stride);
        }

        public GraphicsBuffer Buffer { get; private set; }

        public int Capacity => Buffer.count;

        /// <summary>Makes room for <paramref name="needed"/> elements, at least doubling; true when the buffer was replaced.</summary>
        public bool Reserve(int needed)
        {
            if (needed <= Capacity)
            {
                return false;
            }

            var capacity = Mathf.Max(needed, Capacity * 2);
            Buffer.Release();
            Buffer = new GraphicsBuffer(_target, capacity, _stride);
            return true;
        }

        public void Dispose() => Buffer.Release();
    }
}
