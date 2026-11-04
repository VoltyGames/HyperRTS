using HyperRTS.Simulation.Common.Cells;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>The colour of each cell of a grid, which <see cref="CellTexture.Paint"/> reads for every cell index.</summary>
    public interface ICellColors
    {
        Color32 At(int index);
    }

    /// <summary>One texture over a grid, painted by a Burst job and drawn as a single quad on the ground.</summary>
    public sealed class CellTexture
    {
        private const float Height = 0.1f;

        private Texture2D _texture;
        private Material _material;
        private CellGrid _grid;

        /// <summary>Blue to red as <paramref name="t"/> runs from 0 to 1, see-through.</summary>
        public static Color32 Heat(float t)
        {
            t = math.saturate(t);
            return new Color32((byte)(255f * t), (byte)(255f * (1f - math.abs(2f * t - 1f))), (byte)(255f * (1f - t)), 110);
        }

        /// <summary>A stable colour for an id, with 0 left clear.</summary>
        public static Color32 Hashed(int id)
        {
            if (id == 0)
            {
                return default;
            }

            var bits = math.hash(new int2(id, 7));
            return new Color32((byte)(bits & 255), (byte)((bits >> 8) & 255), (byte)((bits >> 16) & 255), 110);
        }

        /// <summary>Fills the texture from <paramref name="colors"/> over <paramref name="grid"/> and uploads it.</summary>
        public void Paint<T>(in CellGrid grid, in T colors) where T : struct, ICellColors
        {
            Prepare(grid);
            var job = new PaintJob<T> { Colors = colors, Pixels = _texture.GetRawTextureData<Color32>() };
            job.Run(grid.Count);
            _texture.Apply(false);
        }

        /// <summary>Draws the painted cells as one quad over the grid.</summary>
        public void Draw()
        {
            var min = _grid.Min;
            var max = min + (float2)_grid.Size * _grid.CellSize;
            _material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.TexCoord2(0f, 0f);
            GL.Vertex3(min.x, Height, min.y);
            GL.TexCoord2(0f, 1f);
            GL.Vertex3(min.x, Height, max.y);
            GL.TexCoord2(1f, 1f);
            GL.Vertex3(max.x, Height, max.y);
            GL.TexCoord2(1f, 0f);
            GL.Vertex3(max.x, Height, min.y);
            GL.End();
        }

        private void Prepare(in CellGrid grid)
        {
            _grid = grid;
            if (_material == null)
            {
                _material = new Material(Shader.Find("Hidden/HyperRTS/DebugCells")) { hideFlags = HideFlags.HideAndDontSave };
                AssemblyReloadEvents.beforeAssemblyReload += Destroy;
            }

            if (_texture == null || _texture.width != grid.Size.x || _texture.height != grid.Size.y)
            {
                Object.DestroyImmediate(_texture);
                _texture = new Texture2D(grid.Size.x, grid.Size.y, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _material.mainTexture = _texture;
            }
        }

        private void Destroy()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Destroy;
            Object.DestroyImmediate(_texture);
            Object.DestroyImmediate(_material);
        }

        // Run on the main thread: the colours read whole arrays, which a parallel job's index range would forbid.
        [BurstCompile]
        private struct PaintJob<T> : IJobFor where T : struct, ICellColors
        {
            public T Colors;
            public NativeArray<Color32> Pixels;

            public void Execute(int index) => Pixels[index] = Colors.At(index);
        }
    }
}
