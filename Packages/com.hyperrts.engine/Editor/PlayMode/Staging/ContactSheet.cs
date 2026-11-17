using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>Tiles images into one PNG about 1920 px wide, three or four across, without labels; the caller lists the order.</summary>
    internal sealed class ContactSheet
    {
        private const int Width = 1920;

        private readonly List<string> _images = new();

        public IReadOnlyList<string> Images => _images;

        public void Add(string imagePath) => _images.Add(imagePath);

        /// <summary>Adds up to <paramref name="most"/> evenly spaced files of <paramref name="files"/>, first and last included.</summary>
        public void AddSpread(IReadOnlyList<string> files, int most)
        {
            var take = Math.Min(most, files.Count);
            for (var i = 0; i < take; i++)
            {
                _images.Add(files[take == 1 ? 0 : i * (files.Count - 1) / (take - 1)]);
            }
        }

        /// <summary>Writes the sheet; false when there is nothing to tile.</summary>
        public bool Write(string path)
        {
            if (_images.Count == 0)
            {
                return false;
            }

            var columns = _images.Count <= 6 ? 3 : 4;
            var rows = (_images.Count + columns - 1) / columns;
            var tileWidth = Width / columns;
            var tileHeight = 0;
            Color32[] pixels = null;
            for (var i = 0; i < _images.Count; i++)
            {
                var tile = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                try
                {
                    tile.LoadImage(File.ReadAllBytes(_images[i]));
                    if (pixels == null)
                    {
                        tileHeight = Mathf.Max(1, tileWidth * tile.height / tile.width);
                        pixels = new Color32[tileWidth * columns * tileHeight * rows];
                    }

                    Place(pixels, tile, i, columns, rows, new Vector2Int(tileWidth, tileHeight));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tile);
                }
            }

            Save(path, pixels, new Vector2Int(tileWidth * columns, tileHeight * rows));
            return true;
        }

        private static void Save(string path, Color32[] pixels, Vector2Int size)
        {
            var sheet = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            try
            {
                sheet.SetPixels32(pixels);
                File.WriteAllBytes(path, sheet.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }

        // Row 0 is the sheet's bottom, so the first tile goes at the top.
        private static void Place(Color32[] pixels, Texture2D tile, int index, int columns, int rows, Vector2Int size)
        {
            var mip = Mathf.Clamp((int)Mathf.Log(tile.width / (float)size.x, 2f), 0, tile.mipmapCount - 1);
            var left = index % columns * size.x;
            var top = (rows - 1 - index / columns) * size.y;
            for (var y = 0; y < size.y; y++)
            {
                for (var x = 0; x < size.x; x++)
                {
                    var color = tile.GetPixelBilinear((x + 0.5f) / size.x, (y + 0.5f) / size.y, mip);
                    pixels[(top + y) * size.x * columns + left + x] = color;
                }
            }
        }
    }
}
