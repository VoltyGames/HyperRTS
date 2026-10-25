using System.Collections.Generic;
using Unity.Mathematics;

namespace HyperRTS.Editor.Maps.Nav
{
    /// <summary>A path found on a <see cref="MapNavGrid"/>: its cells, length in metres and the search effort.</summary>
    public sealed class MapPath
    {
        public MapPath(List<int2> cells, float length, int expansions)
        {
            Cells = cells;
            Length = length;
            Expansions = expansions;
        }

        public List<int2> Cells { get; }

        public float Length { get; }

        /// <summary>Cells the search expanded.</summary>
        public int Expansions { get; }
    }
}
