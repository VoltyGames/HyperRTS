using System;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering;

/// <summary>A GPU buffer that is replaced by a larger one when more room is needed; contents are not kept.</summary>
internal sealed class GrowingBuffer(GraphicsBuffer.Target target, int stride, int capacity) : IDisposable
{
    private readonly GraphicsBuffer.Target _target = target;
    private readonly int _stride = stride;

    public GraphicsBuffer Buffer { get; private set; } = new GraphicsBuffer(target, Mathf.Max(1, capacity), stride);

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
