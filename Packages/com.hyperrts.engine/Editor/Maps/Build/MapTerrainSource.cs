using System.IO;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// Reads a terrain layer texture's source image from its file (the imported copy may be compressed or capped in
    /// size), resampled to the array slice size.
    /// </summary>
    internal static class MapTerrainSource
    {
        private const float LowPercentile = 0.01f;
        private const float HighPercentile = 0.99f;

        internal static bool IsReadable(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension is ".png" or ".jpg" or ".jpeg";
        }

        internal static Color32[] Read(Texture2D texture, int size)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!image.LoadImage(File.ReadAllBytes(FileUtil.GetPhysicalPath(path))))
                {
                    throw new InvalidDataException($"HyperRTS: terrain texture '{path}' is not a readable PNG or JPG.");
                }

                var exact = image.width == size && image.height == size;
                return exact ? image.GetPixels32() : Resampled(image, size);
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        /// <summary>
        /// A mask's height (blue) stretched so its 1st to 99th percentiles span 0..255: photos with flat or steep
        /// height maps then blend alike, and each layer's height-blend weight means the same thing.
        /// </summary>
        internal static byte[] Heights(Color32[] mask)
        {
            var histogram = new int[256];
            foreach (var texel in mask)
            {
                histogram[texel.b]++;
            }

            var low = Percentile(histogram, mask.Length * LowPercentile);
            var high = Percentile(histogram, mask.Length * HighPercentile);
            var heights = new byte[mask.Length];
            for (var i = 0; i < mask.Length; i++)
            {
                var height = high > low ? Mathf.InverseLerp(low, high, mask[i].b) : 0.5f;
                heights[i] = (byte)Mathf.RoundToInt(height * 255f);
            }

            return heights;
        }

        private static int Percentile(int[] histogram, float count)
        {
            var seen = 0;
            for (var value = 0; value < histogram.Length; value++)
            {
                seen += histogram[value];
                if (seen >= count)
                {
                    return value;
                }
            }

            return histogram.Length - 1;
        }

        /// <summary>A GPU blit; the mips let it average every texel a target pixel covers when it shrinks the image.</summary>
        private static Color32[] Resampled(Texture2D image, int size)
        {
            var source = new Texture2D(image.width, image.height, TextureFormat.RGBA32, true, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Repeat,
            };
            source.SetPixels32(image.GetPixels32());
            source.Apply(true);
            var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, target);

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            var pixels = result.GetPixels32();
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
            return pixels;
        }
    }
}
