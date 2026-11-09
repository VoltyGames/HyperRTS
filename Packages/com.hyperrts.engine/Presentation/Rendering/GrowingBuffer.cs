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

        /// <summary>Makes room for <paramref name="needed"/> elements and a quarter more; true when the buffer was replaced.</summary>
        public bool Reserve(int needed)
        {
            if (needed <= Buffer.count)
            {
                return false;
            }

            Buffer.Release();
            Buffer = new GraphicsBuffer(_target, needed + needed / 4, _stride);
            return true;
        }

        public void Dispose() => Buffer.Release();
    }
}
