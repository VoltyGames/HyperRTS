using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// One frame's instances of one mesh, each with two per-instance vectors the shader reads as instanced
    /// properties, so one draw covers every colour and size.
    /// </summary>
    public sealed class InstanceDataBatch
    {
        private const int MaxPerCall = 1023;

        // Shaders move these instances (onto the ground, out to screen offsets), so their matrices can't cull them.
        private static readonly Bounds Everywhere = new(Vector3.zero, Vector3.one * 100000f);

        private readonly List<Matrix4x4> _matrices = new();
        private readonly List<Vector4> _first = new();
        private readonly List<Vector4> _second = new();
        private readonly List<MaterialPropertyBlock> _blocks = new();
        private readonly Vector4[] _chunkFirst = new Vector4[MaxPerCall];
        private readonly Vector4[] _chunkSecond = new Vector4[MaxPerCall];
        private readonly Mesh _mesh;
        private readonly int _firstId;
        private readonly int _secondId;

        public InstanceDataBatch(Mesh mesh, string firstProperty, string secondProperty)
        {
            _mesh = mesh;
            _firstId = Shader.PropertyToID(firstProperty);
            _secondId = Shader.PropertyToID(secondProperty);
        }

        public int Count => _matrices.Count;

        public void Add(in Matrix4x4 matrix, Vector4 first, Vector4 second)
        {
            _matrices.Add(matrix);
            _first.Add(first);
            _second.Add(second);
        }

        /// <summary>Draws this frame's instances, then clears them.</summary>
        public void Draw(Material material, int layer)
        {
            for (int start = 0, chunk = 0; start < _matrices.Count; start += MaxPerCall, chunk++)
            {
                var count = Mathf.Min(MaxPerCall, _matrices.Count - start);
                _first.CopyTo(start, _chunkFirst, 0, count);
                _second.CopyTo(start, _chunkSecond, 0, count);
                var block = Block(chunk);
                block.SetVectorArray(_firstId, _chunkFirst);
                block.SetVectorArray(_secondId, _chunkSecond);
                var parameters = new RenderParams(material)
                {
                    matProps = block,
                    worldBounds = Everywhere,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    layer = layer,
                };
                Graphics.RenderMeshInstanced(parameters, _mesh, 0, _matrices, count, start);
            }

            _matrices.Clear();
            _first.Clear();
            _second.Clear();
        }

        // A block per chunk keeps each draw's values; arrays fix their length on first set, so each upload is full length.
        private MaterialPropertyBlock Block(int chunk)
        {
            while (_blocks.Count <= chunk)
            {
                _blocks.Add(new MaterialPropertyBlock());
            }

            return _blocks[chunk];
        }
    }
}
