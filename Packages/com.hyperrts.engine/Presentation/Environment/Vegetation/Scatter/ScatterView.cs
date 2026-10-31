using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Fire;
using HyperRTS.Presentation.Environment.Interaction;
using HyperRTS.Presentation.Environment.Vegetation.Grass;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>
    /// What a camera's scattered plants were last grown for: its spot, settings and visible tiles, the buildings and
    /// the ash, and for plants that bend under feet the trample map. A still camera then only redraws.
    /// </summary>
    internal sealed class ScatterView
    {
        // Metres the camera may drift before its plants regrow; fades and bands lag by at most this.
        private const float Step = 0.5f;

        private readonly List<int> _tiles = new();
        private readonly bool _readsTrample;
        private Vector3Int _cell;
        private Vector4 _settings;
        private int _footprints;
        private int _burntCells;
        private bool _grown;

        /// <summary>A view for plants that bend under the trample map when <paramref name="readsTrample"/>, which changes every frame it is published.</summary>
        public ScatterView(bool readsTrample) => _readsTrample = readsTrample;

        /// <summary>Whether the plants already hold this view: same spot, settings and visible tiles, no new buildings or ash.</summary>
        public bool Holds(Vector3 position, ScatterSettings settings, List<int> tiles)
        {
            if (!_grown || settings.Packed != _settings || _footprints != BuildingFootprintMask.Version)
            {
                return false;
            }

            if (_burntCells != FieldFireSystem.BurntCells || (_readsTrample && InteractionSurfaces.Published))
            {
                return false;
            }

            return Cell(position) == _cell && SameTiles(tiles);
        }

        /// <summary>Records the view the plants were just grown for.</summary>
        public void Remember(Vector3 position, ScatterSettings settings, List<int> tiles)
        {
            _cell = Cell(position);
            _settings = settings.Packed;
            _footprints = BuildingFootprintMask.Version;
            _burntCells = FieldFireSystem.BurntCells;
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
