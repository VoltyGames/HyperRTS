using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// One frame's instances of one mesh, drawn unshadowed in one indirect call. The shader reads them as
    /// <c>StructuredBuffer&lt;T&gt; _Instances</c> by <c>SV_InstanceID</c>; the buffer doubles whenever it fills.
    /// </summary>
    public sealed class InstanceBuffer<T> : IDisposable where T : unmanaged
    {
        private const int StartCapacity = 64;
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");

        // Shaders move these instances (onto the ground, out to screen offsets), so no bounds of ours can cull them.
        private static readonly Bounds Everywhere = new(Vector3.zero, Vector3.one * 100000f);

        private readonly Mesh _mesh;
        private readonly GraphicsBuffer _args;
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _command = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        private T[] _staged = new T[StartCapacity];
        private GraphicsBuffer _instances;

        public InstanceBuffer(Mesh mesh)
        {
            _mesh = mesh;
            _args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _command[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = mesh.GetIndexCount(0),
                startIndex = mesh.GetIndexStart(0),
                baseVertexIndex = mesh.GetBaseVertex(0),
            };
        }

        public int Count { get; private set; }

        /// <summary>Per-draw values beside the instances, such as material overrides.</summary>
        public MaterialPropertyBlock Properties { get; } = new();

        public void Add(in T instance)
        {
            if (Count == _staged.Length)
            {
                Array.Resize(ref _staged, Count * 2);
            }

            _staged[Count++] = instance;
        }

        /// <summary>Draws this frame's instances on <paramref name="layer"/>, then clears them.</summary>
        public void Draw(Material material, int layer)
        {
            if (Count == 0)
            {
                return;
            }

            Upload();
            var parameters = new RenderParams(material)
            {
                matProps = Properties,
                worldBounds = Everywhere,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = layer,
            };
            Graphics.RenderMeshIndirect(parameters, _mesh, _args);
            Count = 0;
        }

        public void Dispose()
        {
            _instances?.Release();
            _args.Release();
        }

        private void Upload()
        {
            if (_instances == null || _instances.count < Count)
            {
                _instances?.Release();
                _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _staged.Length, UnsafeUtility.SizeOf<T>());
                Properties.SetBuffer(InstancesId, _instances);
            }

            _instances.SetData(_staged, 0, 0, Count);
            _command[0].instanceCount = (uint)Count;
            _args.SetData(_command);
        }
    }
}
