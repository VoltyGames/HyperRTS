using HyperRTS.Editor.Maps.Data;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// Bakes the map's top-down ground colour, the picture foliage blends into at its roots: each texel mixes the
    /// layers' mean albedo by splat weight ([z, x, layer] over the whole terrain), tinted by the macro picture over
    /// the same area, where RGB 0.5 leaves the ground unchanged (null for no macro).
    /// </summary>
    public static class MapGroundColor
    {
        /// <summary>Splat texels per side of a ground texel: the splat runs 2.5 to 5 texels per metre.</summary>
        private const int Downsample = 2;

        /// <summary>Mean colours are read from a small blit; its mips do the averaging.</summary>
        private const int MeanSize = 64;

        public static Texture2D Write(string path, float[,,] splat, MapClimate climate, Texture2D macro)
        {
            var size = splat.GetLength(0) / Downsample;
            var albedo = LayerAlbedo(climate, splat.GetLength(2));
            var tint = Read(macro, size);
            var pixels = new Color32[size * size];
            for (var z = 0; z < size; z++)
            {
                for (var x = 0; x < size; x++)
                {
                    var color = Ground(splat, albedo, x, z) * tint[z * size + x] * 2f;
                    color.a = 1f;
                    pixels[z * size + x] = color.gamma;
                }
            }

            Save(path, pixels, size);
            return Import(path, size);
        }

        /// <summary>The linear mean albedo of each layer's base map, tinted the way the terrain tints it.</summary>
        private static Color[] LayerAlbedo(MapClimate climate, int count)
        {
            var albedo = new Color[count];
            for (var i = 0; i < count; i++)
            {
                var layer = climate.layers[i].terrainLayer;
                var pixels = Read(layer.diffuseTexture, MeanSize);
                var sum = Color.clear;
                foreach (var pixel in pixels)
                {
                    sum += pixel;
                }

                var remap = layer.diffuseRemapMax;
                albedo[i] = sum / pixels.Length * new Color(remap.x, remap.y, remap.z, 1f);
            }

            return albedo;
        }

        /// <summary>The box average of the splat texels under one ground texel, mixed into a colour.</summary>
        private static Color Ground(float[,,] splat, Color[] albedo, int x, int z)
        {
            var color = Color.clear;
            for (var dz = 0; dz < Downsample; dz++)
            {
                for (var dx = 0; dx < Downsample; dx++)
                {
                    for (var layer = 0; layer < albedo.Length; layer++)
                    {
                        color += albedo[layer] * splat[z * Downsample + dz, x * Downsample + dx, layer];
                    }
                }
            }

            return color / (Downsample * Downsample);
        }

        /// <summary>
        /// Any texture, readable or not, resampled to size × size linear colours by the GPU (sRGB textures come back
        /// decoded, as shaders see them).
        /// </summary>
        private static Color[] Read(Texture texture, int size)
        {
            var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var copy = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var active = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                copy.Apply(false);
                return copy.GetPixels();
            }
            finally
            {
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(copy);
            }
        }

        private static void Save(string path, Color32[] pixels, int size)
        {
            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            image.SetPixels32(pixels);
            image.Apply(false);
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        private static Texture2D Import(string path, int size)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(size);
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
