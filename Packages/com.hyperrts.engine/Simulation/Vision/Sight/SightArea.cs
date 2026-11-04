using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>
    /// A viewer's sight as last drawn: the square of fog cells round it in <see cref="SightAreaBits"/>, kept until it
    /// moves to another cell, its eye, reach or team changes, the grid is built anew or blockers change round it.
    /// </summary>
    public struct SightArea : IComponentData
    {
        public const int Ground = 0;
        public const int Tall = 1;
        public const int Close = 2;
        public const int Layers = 3;

        public int2 Center;

        /// <summary>Cells from the centre to the square's edge; below 0 for a viewer that sees nothing.</summary>
        public int Radius;

        public float Eye;
        public float Range;
        public float CloseRange;
        public byte Team;
        public int FogGeneration;
        public int BlockersGeneration;

        public readonly int Width => 2 * Radius + 1;

        public readonly int RowWords => (Width + 63) >> 6;

        public readonly int2 Origin => Center - Radius;

        public readonly int Words => Layers * Width * RowWords;

        /// <summary>Marks a fog cell in one layer; cells off the square are ignored.</summary>
        public readonly void Set(NativeArray<ulong> bits, int layer, int2 cell)
        {
            var local = cell - Origin;
            if (math.any(local < 0) || math.any(local >= Width))
            {
                return;
            }

            bits[Word(layer, local.y, local.x >> 6)] |= 1ul << (local.x & 63);
        }

        public readonly int Word(int layer, int row, int word) => (layer * Width + row) * RowWords + word;

        /// <summary>Whether it was drawn for this place, eye, reach and team on the current grids.</summary>
        public readonly bool Matches(in SightArea other)
        {
            var place = Center.Equals(other.Center) && Radius == other.Radius && Team == other.Team;
            var reach = Eye == other.Eye && Range == other.Range && CloseRange == other.CloseRange;
            var grids = FogGeneration == other.FogGeneration && BlockersGeneration == other.BlockersGeneration;
            return place && reach && grids;
        }
    }

    /// <summary>The bits of a <see cref="SightArea"/>: per layer, per row, the row's 64-cell words.</summary>
    [InternalBufferCapacity(0)]
    public struct SightAreaBits : IBufferElementData
    {
        public ulong Value;
    }
}
