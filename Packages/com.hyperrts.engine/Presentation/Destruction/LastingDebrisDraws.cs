using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Draws <see cref="LastingDebris"/> through a BatchRendererGroup with indirect draws, one per mesh and material,
    /// reading each piece's matrices from a GPU buffer written only when a piece settles, sinks or goes.
    /// </summary>
    internal sealed class LastingDebrisDraws : IDisposable
    {
        private const int MatrixSize = 48;

        // Address 0 must read as zero for the shader's defaults.
        private const int HeaderMatrices = 2;

        private static readonly int ObjectToWorldId = Shader.PropertyToID("unity_ObjectToWorld");
        private static readonly int WorldToObjectId = Shader.PropertyToID("unity_WorldToObject");

        private readonly int _capacity;
        private readonly BatchRendererGroup _group;
        private readonly GraphicsBuffer _instances;
        private readonly BatchID _batch;
        private readonly float3x4[] _objectToWorld;
        private readonly float3x4[] _worldToObject;
        private readonly List<LastingDebrisBatch> _batches = new();
        private readonly int[] _batchOf;
        private readonly int[] _listIndex;
        private GraphicsBuffer _visible;
        private GraphicsBuffer _commands;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] _commandData;
        private bool _commandsChanged;
        private int _changedFirst = int.MaxValue;
        private int _changedLast = -1;

        public LastingDebrisDraws(int capacity)
        {
            _capacity = capacity;
            _objectToWorld = new float3x4[capacity];
            _worldToObject = new float3x4[capacity];
            _batchOf = new int[capacity];
            _listIndex = new int[capacity];
            _group = new BatchRendererGroup(Cull, IntPtr.Zero);
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
            Write(slot, matrix);
            var batch = BatchIndex(mesh, material);
            _batchOf[slot] = batch;
            _listIndex[slot] = _batches[batch].Add(slot);
            _commandsChanged = true;
        }

        public void Move(int slot, float4x4 matrix) => Write(slot, matrix);

        public void Remove(int slot)
        {
            var moved = _batches[_batchOf[slot]].Remove(_listIndex[slot]);
            _listIndex[moved] = _listIndex[slot];
            _commandsChanged = true;
        }

        /// <summary>Writes this frame's changes to the GPU; nothing when no piece settled, sank or went.</summary>
        public void Upload()
        {
            if (_changedLast >= 0)
            {
                var count = _changedLast - _changedFirst + 1;
                _instances.SetData(_objectToWorld, _changedFirst, HeaderMatrices + _changedFirst, count);
                _instances.SetData(_worldToObject, _changedFirst, HeaderMatrices + _capacity + _changedFirst, count);
                (_changedFirst, _changedLast) = (int.MaxValue, -1);
            }

            for (var index = 0; index < _batches.Count; index++)
            {
                var batch = _batches[index];
                if (batch.Changed)
                {
                    _visible.SetData(batch.Slots, 0, index * _capacity, batch.Count);
                    batch.Changed = false;
                }
            }

            if (_commandsChanged)
            {
                for (var index = 0; index < _batches.Count; index++)
                {
                    _commandData[index] = _batches[index].Command;
                }

                _commands.SetData(_commandData);
                _commandsChanged = false;
            }
        }

        public void Dispose()
        {
            _group.Dispose();
            _instances.Release();
            _visible?.Release();
            _commands?.Release();
        }

        private static uint PerInstance(int matrix) => 0x80000000u | (uint)(matrix * MatrixSize);

        private void Write(int slot, float4x4 matrix)
        {
            _objectToWorld[slot] = Packed(matrix);
            _worldToObject[slot] = Packed(math.inverse(matrix));
            _changedFirst = math.min(_changedFirst, slot);
            _changedLast = math.max(_changedLast, slot);
        }

        private static float3x4 Packed(float4x4 matrix) => new(matrix.c0.xyz, matrix.c1.xyz, matrix.c2.xyz, matrix.c3.xyz);

        private int BatchIndex(Mesh mesh, Material material)
        {
            for (var index = 0; index < _batches.Count; index++)
            {
                if (_batches[index].Draws(mesh, material))
                {
                    return index;
                }
            }

            _batches.Add(new LastingDebrisBatch(mesh, material, _group.RegisterMesh(mesh), _group.RegisterMaterial(material), _capacity));
            GrowCommandBuffers();
            return _batches.Count - 1;
        }

        // A new batch needs its own room in the slot list and its own command, so every list is written again.
        private void GrowCommandBuffers()
        {
            _visible?.Release();
            _commands?.Release();
            _visible = new GraphicsBuffer(GraphicsBuffer.Target.Raw, _batches.Count * _capacity, sizeof(uint));
            _commands = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, _batches.Count,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[_batches.Count];
            foreach (var batch in _batches)
            {
                batch.Changed = true;
            }
        }

        private unsafe JobHandle Cull(BatchRendererGroup group, BatchCullingContext context, BatchCullingOutput output, IntPtr user)
        {
            var drawn = 0;
            foreach (var batch in _batches)
            {
                drawn += batch.Count > 0 ? 1 : 0;
            }

            if (drawn == 0)
            {
                return default;
            }

            var commands = Allocate<BatchDrawCommandIndirect>(drawn);
            var next = 0;
            for (var index = 0; index < _batches.Count; index++)
            {
                if (_batches[index].Count > 0)
                {
                    commands[next++] = DrawCommand(index);
                }
            }

            var range = Allocate<BatchDrawRange>(1);
            range[0] = new BatchDrawRange
            {
                drawCommandsType = BatchDrawCommandType.Indirect,
                drawCommandsCount = (uint)drawn,
                filterSettings = new BatchFilterSettings
                {
                    renderingLayerMask = 1,
                    motionMode = MotionVectorGenerationMode.Camera,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                },
            };
            output.drawCommands[0] = new BatchCullingOutputDrawCommands
            {
                indirectDrawCommands = commands,
                indirectDrawCommandCount = drawn,
                drawRanges = range,
                drawRangeCount = 1,
            };
            return default;
        }

        private BatchDrawCommandIndirect DrawCommand(int index) => new()
        {
            visibleOffset = (uint)(index * _capacity),
            batchID = _batch,
            materialID = _batches[index].MaterialID,
            meshID = _batches[index].MeshID,
            topology = MeshTopology.Triangles,
            splitVisibilityMask = 0xff,
            visibleInstancesBufferHandle = _visible.bufferHandle,
            indirectArgsBufferHandle = _commands.bufferHandle,
            indirectArgsBufferOffset = (uint)(index * GraphicsBuffer.IndirectDrawIndexedArgs.size),
        };

        private static unsafe T* Allocate<T>(int count) where T : unmanaged =>
            (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), Allocator.TempJob);
    }
}
