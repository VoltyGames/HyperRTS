using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>The view a camera's scattered plants were last grown for, so a still camera only redraws.</summary>
    internal sealed class ScatterView
    {
        // Metres the camera may drift before its plants regrow; fades and bands lag by at most this.
        private const float Step = 0.5f;

        private readonly List<int> _tiles = new();
        private Vector3Int _cell;
        private Vector4 _settings;
        private bool _grown;

        /// <summary>Whether the plants already hold this view: same spot, settings and visible tiles.</summary>
        public bool Holds(Vector3 position, Vector4 settings, List<int> tiles)
        {
            if (!_grown || settings != _settings)
            {
                return false;
            }

            return Cell(position) == _cell && SameTiles(tiles);
        }

        /// <summary>Records the view the plants were just grown for.</summary>
        public void Remember(Vector3 position, Vector4 settings, List<int> tiles)
        {
            _cell = Cell(position);
            _settings = settings;
            _tiles.Clear();
            _tiles.AddRange(tiles);
            _grown = true;
        }

        private bool SameTiles(List<int> tiles)
        {
            if (tiles.Count != _tiles.Count)
            {
                return false;
            }

            for (var i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] != _tiles[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3Int Cell(Vector3 position) => Vector3Int.RoundToInt(position / Step);
    }
}
