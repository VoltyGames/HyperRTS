using System.Collections.Generic;
using HyperRTS.Editor.Maps.Layout;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Hints
{
    /// <summary>One slot's base while the hints are computed: where it is on the nav grid and how far everything is to walk.</summary>
    public sealed class MapHintBase
    {
        public MapHintBase(MapSpawn spawn, int2 cell, int cellIndex, float[] walk)
        {
            Slot = spawn.Slot;
            Team = spawn.Team;
            Position = spawn.Position;
            Cell = cell;
            CellIndex = cellIndex;
            Walk = walk;
        }

        public int Slot { get; }

        public int Team { get; }

        public Vector2 Position { get; }

        /// <summary>The walkable cell nearest the base.</summary>
        public int2 Cell { get; }

        public int CellIndex { get; }

        /// <summary>Metres to walk from the base to each nav cell by index; infinity where unreachable.</summary>
        public float[] Walk { get; }

        public List<Vector2> Expansions { get; } = new();

        public List<Vector2> Defence { get; } = new();
    }
}
