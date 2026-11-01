using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Draws <see cref="LastingDebris"/> through a BatchRendererGroup, culled per piece in each view by
    /// <see cref="LastingDebrisCulling"/>, reading each piece's matrices from a GPU buffer written only when a piece
    /// settles, sinks or goes.
    /// </summary>
    internal sealed class LastingDebrisDraws : IDisposable
    {
        private const int MatrixSize = 48;

        // Address 0 must read as zero for the shader's defaults.
        private const int HeaderMatrices = 2;

        private static readonly int ObjectToWorldId = Shader.PropertyToID("unity_ObjectToWorld");
        private static readonly int WorldToObjectId = Shader.PropertyToID("unity_WorldToObject");

        private static readonly BatchFilterSettings Filter = new()
        {
            renderingLayerMask = 1,
            motionMode = MotionVectorGenerationMode.Camera,
            shadowCastingMode = ShadowCastingMode.On,
            receiveShadows = true,
        };

        private readonly int _capacity;
        private readonly BatchRendererGroup _group;
        private readonly GraphicsBuffer _instances;
        private readonly BatchID _batch;
        private readonly float3x4[] _objectToWorld;
        private readonly float3x4[] _worldToObject;
        private readonly Dictionary<(Mesh, Material), int> _batchIndex = new();
        private readonly int[] _batchOf;
        private readonly int[] _listIndex;
        private NativeList<LastingDebrisBatch> _batches = new(Allocator.Persistent);
        private NativeList<int> _members = new(Allocator.Persistent);
        private NativeArray<float4> _bounds;
        private JobHandle _culling;
        private int _changedFirst = int.MaxValue;
        private int _changedLast = -1;

        public LastingDebrisDraws(int capacity)
        {
            _capacity = capacity;
            _objectToWorld = new float3x4[capacity];
            _worldToObject = new float3x4[capacity];
            _batchOf = new int[capacity];
            _listIndex = new int[capacity];
            _bounds = new NativeArray<float4>(capacity, Allocator.Persistent);
            _group = new BatchRendererGroup(Cull, IntPtr.Zero);

            // Unity culls the whole group against these bounds before the callback, and pieces lie anywhere on the map.
            _group.SetGlobalBounds(new Bounds(Vector3.zero, Vector3.one * 100000f));
            var bytes = (HeaderMatrices + 2 * capacity) * MatrixSize;
            _instances = new GraphicsBuffer(GraphicsBuffer.Target.Raw, bytes / sizeof(int), sizeof(int));
            _instances.SetData(new float3x4[HeaderMatrices]);
            var metadata = new NativeArray<MetadataValue>(2, Allocator.Temp);
            metadata[0] = new MetadataValue { NameID = ObjectToWorldId, Value = PerInstance(HeaderMatrices) };
            metadata[1] = new MetadataValue { NameID = WorldToObjectId, Value = PerInstance(HeaderMatrices + capacity) };
            _batch = _group.AddBatch(metadata, _instances.bufferHandle);
        }

        public void Place(int slot, Mesh mesh, Material material, float4x4 matrix)
        {
            _culling.Complete();
            var batch = BatchIndex(mesh, material);
            ref var entry = ref _batches.ElementAt(batch);
            _members[batch * _capacity + entry.Count] = slot;
            _listIndex[slot] = entry.Count++;
            _batchOf[slot] = batch;
            Write(slot, matrix);
        }

        public void Move(int slot, float4x4 matrix)
        {
            _culling.Complete();
            Write(slot, matrix);
        }

        // The batch's last slot fills the gap, so its list stays packed.
        public void Remove(int slot)
        {
            _culling.Complete();
            var batch = _batchOf[slot];
            ref var entry = ref _batches.ElementAt(batch);
            var moved = _members[batch * _capacity + --entry.Count];
            _members[batch * _capacity + _listIndex[slot]] = moved;
            _listIndex[moved] = _listIndex[slot];
        }

        /// <summary>Writes this frame's changes to the GPU; nothing when no piece settled, sank or went.</summary>
        public void Upload()
        {
            if (_changedLast < 0)
            {
                return;
            }

            var count = _changedLast - _changedFirst + 1;
            _instances.SetData(_objectToWorld, _changedFirst, HeaderMatrices + _changedFirst, count);
            _instances.SetData(_worldToObject, _changedFirst, HeaderMatrices + _capacity + _changedFirst, count);
            (_changedFirst, _changedLast) = (int.MaxValue, -1);
        }

        public void Dispose()
        {
            _culling.Complete();
            _group.Dispose();
            _instances.Release();
            _batches.Dispose();
            _members.Dispose();
            _bounds.Dispose();
        }

        private static uint PerInstance(int matrix) => 0x80000000u | (uint)(matrix * MatrixSize);

        private void Write(int slot, float4x4 matrix)
        {
            _objectToWorld[slot] = Packed(matrix);
            _worldToObject[slot] = Packed(math.inverse(matrix));
            _bounds[slot] = _batches[_batchOf[slot]].Sphere(matrix);
            _changedFirst = math.min(_changedFirst, slot);
            _changedLast = math.max(_changedLast, slot);
        }

        private static float3x4 Packed(float4x4 matrix) => new(matrix.c0.xyz, matrix.c1.xyz, matrix.c2.xyz, matrix.c3.xyz);

        private int BatchIndex(Mesh mesh, Material material)
        {
            if (_batchIndex.TryGetValue((mesh, material), out var index))
            {
                return index;
            }

            index = _batches.Length;
            _batchIndex.Add((mesh, material), index);
            _batches.Add(new LastingDebrisBatch
            {
                MeshID = _group.RegisterMesh(mesh),
                MaterialID = _group.RegisterMaterial(material),
                MeshBounds = mesh.bounds,
            });
            _members.Resize(_batches.Length * _capacity, NativeArrayOptions.UninitializedMemory);
            return index;
        }

        private JobHandle Cull(BatchRendererGroup group, BatchCullingContext context, BatchCullingOutput output, IntPtr user)
        {
            if (!AnyPlaced())
            {
                return default;
            }

            var job = new LastingDebrisCulling
            {
                Batches = _batches.AsArray(),
                Members = _members.AsArray(),
                Bounds = _bounds,
                Planes = context.cullingPlanes.Reinterpret<float4>(),
                Splits = context.cullingSplits,
                Capacity = _capacity,
                Batch = _batch,
                Filter = Filter,
                Output = output.drawCommands,
            };
            var handle = job.Schedule();
            _culling = JobHandle.CombineDependencies(_culling, handle);
            return handle;
        }

        private bool AnyPlaced()
        {
            foreach (var batch in _batches)
            {
                if (batch.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
