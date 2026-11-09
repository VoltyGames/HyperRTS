using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Creates, frees and fills the colour-only render textures that map and weather textures are drawn into.</summary>
    public static class RenderTargets
    {
        /// <summary>A created colour-only render texture that never saves with a scene.</summary>
        public static RenderTexture Create(string name, int width, int height, GraphicsFormat format, FilterMode filter,
            TextureWrapMode wrap)
        {
            var texture = new RenderTexture(width, height, format, GraphicsFormat.None, 1)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                filterMode = filter,
                wrapMode = wrap,
            };
            texture.Create();
            return texture;
        }

        /// <summary>Frees the texture's GPU memory and destroys it.</summary>
        public static void Free(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            CoreUtils.Destroy(texture);
        }

        /// <summary>One triangle that covers the whole target.</summary>
        public static void DrawFullScreen(this CommandBuffer commands, Material material, int pass) =>
            commands.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3);
    }
}
