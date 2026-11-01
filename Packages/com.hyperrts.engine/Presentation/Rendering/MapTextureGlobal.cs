using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// A world-aligned texture shader global and the rect global that places it over the map. Shaders read a zero rect
    /// as nothing published.
    /// </summary>
    public readonly struct MapTextureGlobal
    {
        public readonly int TextureId;
        public readonly int RectId;

        public MapTextureGlobal(string texture, string rect)
        {
            TextureId = Shader.PropertyToID(texture);
            RectId = Shader.PropertyToID(rect);
        }

        public bool Published => Shader.GetGlobalVector(RectId).z > 0f;

        public void Publish(Texture texture, float4 rect)
        {
            Shader.SetGlobalTexture(TextureId, texture);
            Shader.SetGlobalVector(RectId, rect);
        }

        /// <summary>Black and a zero rect, since globals outlive the scene and Play mode in the editor.</summary>
        public void Clear()
        {
            Shader.SetGlobalTexture(TextureId, Texture2D.blackTexture);
            Shader.SetGlobalVector(RectId, Vector4.zero);
        }

        /// <summary>Copies the global to a kernel under the same names: compute shaders don't see globals.</summary>
        public void Bind(ComputeShader compute, int kernel) => Bind(compute, kernel, TextureId, RectId);

        /// <summary>Copies the global to a kernel under its own names; black and a zero rect while none is published.</summary>
        public void Bind(ComputeShader compute, int kernel, int textureId, int rectId)
        {
            var texture = Shader.GetGlobalTexture(TextureId);
            var on = texture != null && Published;
            compute.SetTexture(kernel, textureId, on ? texture : Texture2D.blackTexture);
            compute.SetVector(rectId, on ? Shader.GetGlobalVector(RectId) : Vector4.zero);
        }
    }
}
