using System;
using System.Numerics;

namespace HyperRTS.Editor.Environment
{
    /// <summary>Radix-2 inverse fast Fourier transforms for the editor's tiling texture generators.</summary>
    public static class Fft
    {
        /// <summary>In place, unnormalised, over a size × size grid stored row by row; size is a power of two.</summary>
        public static void Inverse2D(Complex[] grid, int size)
        {
            if (size < 2 || (size & (size - 1)) != 0)
            {
                throw new ArgumentException($"FFT size {size} must be a power of two.", nameof(size));
            }

            var line = new Complex[size];
            for (var y = 0; y < size; y++)
            {
                Array.Copy(grid, y * size, line, 0, size);
                Inverse(line);
                Array.Copy(line, 0, grid, y * size, size);
            }

            for (var x = 0; x < size; x++)
            {
                for (var y = 0; y < size; y++)
                {
                    line[y] = grid[y * size + x];
                }

                Inverse(line);
                for (var y = 0; y < size; y++)
                {
                    grid[y * size + x] = line[y];
                }
            }
        }

        /// <summary>One line in place, unnormalised: sum over k of a_k e^(2 pi i k n / N).</summary>
        public static void Inverse(Complex[] values)
        {
            var count = values.Length;
            for (int i = 1, j = 0; i < count; i++)
            {
                var bit = count >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                {
                    j ^= bit;
                }

                j ^= bit;
                if (i < j)
                {
                    (values[i], values[j]) = (values[j], values[i]);
                }
            }

            for (var length = 2; length <= count; length <<= 1)
            {
                var angle = 2.0 * Math.PI / length;
                var step = new Complex(Math.Cos(angle), Math.Sin(angle));
                for (var start = 0; start < count; start += length)
                {
                    var turn = Complex.One;
                    for (var k = 0; k < length / 2; k++)
                    {
                        var even = values[start + k];
                        var odd = values[start + k + length / 2] * turn;
                        values[start + k] = even + odd;
                        values[start + k + length / 2] = even - odd;
                        turn *= step;
                    }
                }
            }
        }
    }
}
