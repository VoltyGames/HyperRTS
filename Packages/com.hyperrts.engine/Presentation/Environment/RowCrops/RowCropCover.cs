using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// Ground a map's row crops cover, to the <see cref="RowCropFields.TileSize"/> tile: a CPU lookup for client effects
    /// that keep off the fields (the field-id picture lives on the GPU only).
    /// </summary>
    public sealed class RowCropCover
    {
        /// <summary>A map without row crops.</summary>
        public static readonly RowCropCover None = new(float2.zero, int2.zero, new bool[0]);

        private readonly float2 _min;
        private readonly int2 _size;
        private readonly bool[] _sown;

        private RowCropCover(float2 min, int2 size, bool[] sown)
        {
            _min = min;
            _size = size;
            _sown = sown;
        }

        /// <summary>The cover of the drawing <see cref="RowCropRenderer"/>, or <see cref="None"/>.</summary>
        public static RowCropCover Active { get; internal set; } = None;

        /// <summary>Marks every tile that holds at least one field.</summary>
        public static RowCropCover Build(RowCropFields fields)
        {
            var size = new int2(
                (int)math.ceil(fields.area.width / RowCropFields.TileSize),
                (int)math.ceil(fields.area.height / RowCropFields.TileSize));
            var sown = new bool[size.x * size.y];
            foreach (var tile in fields.tiles)
            {
                sown[tile.z * size.x + tile.x] = tile.fieldCount > 0;
            }

            return new RowCropCover(new float2(fields.area.xMin, fields.area.yMin), size, sown);
        }

        /// <summary>True when the tile under world <paramref name="xz"/> holds a crop field.</summary>
        public bool Covers(float2 xz)
        {
            var cell = (int2)math.floor((xz - _min) / RowCropFields.TileSize);
            var inside = math.all(cell >= 0) && math.all(cell < _size);
            if (!inside)
            {
                return false;
            }

            return _sown[cell.y * _size.x + cell.x];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active = None;
    }
}
