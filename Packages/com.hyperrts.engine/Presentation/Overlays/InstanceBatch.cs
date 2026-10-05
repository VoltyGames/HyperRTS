using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>Instances sharing one mesh and colour, drawn with as few instanced calls as possible.</summary>
    public sealed class InstanceBatch
    {
        private const int MaxPerCall = 1023;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly List<Matrix4x4> _matrices = new();
        private readonly MaterialPropertyBlock _properties = new();
        private readonly Mesh _mesh;

        public InstanceBatch(Mesh mesh)
        {
            _mesh = mesh;
        }

        public void Add(in Matrix4x4 matrix) => _matrices.Add(matrix);

        /// <summary>Draws this frame's instances, then clears them.</summary>
        public void Draw(Material material, Color color, int layer)
        {
            if (_matrices.Count == 0)
            {
                return;
            }

            _properties.SetColor(BaseColorId, color);
            var parameters = new RenderParams(material)
            {
                matProps = _properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = layer,
            };

            for (var start = 0; start < _matrices.Count; start += MaxPerCall)
            {
                var count = Mathf.Min(MaxPerCall, _matrices.Count - start);
                Graphics.RenderMeshInstanced(parameters, _mesh, 0, _matrices, count, start);
            }

            _matrices.Clear();
        }
    }
}
