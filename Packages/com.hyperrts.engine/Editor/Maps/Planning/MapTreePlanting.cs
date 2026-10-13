using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>Plants trees at authored-sector points in every symmetry copy, clear of roads, water, steep ground and placements.</summary>
    public sealed class MapTreePlanting
    {
        private const int ScatterAttempts = 6;
        private readonly MapSymmetry _symmetry;
        private readonly MapClimate _climate;
        private readonly MapTreeSpacing _spacing;

        public MapTreePlanting(MapSymmetry symmetry, MapClimate climate, MapTreeSpacing spacing)
        {
            _symmetry = symmetry;
            _climate = climate;
            _spacing = spacing;
        }

        /// <summary>Every tree planted so far, copies included.</summary>
        public List<MapTree> Planted { get; } = new();

        public bool Fits(Vector2 point, float spacing) => _spacing.Fits(point, spacing);

        /// <summary>Plants a tree at a sector point in every copy.</summary>
        public void Plant(int tree, Vector2 sectorPoint, System.Random random) =>
            PlantCopies(tree, sectorPoint, Scale(tree, random), random.Yaw());

        public void PlantCopies(int tree, Vector2 sectorPoint, float scale, float yaw)
        {
            var variant = _climate.trees[tree].Variant(MapTree.SeedAt(sectorPoint));
            foreach (var copy in _symmetry.Copies)
            {
                var placed = copy.Apply(sectorPoint);
                _spacing.Add(placed);
                Planted.Add(new MapTree(tree, placed, scale, copy.ApplyYaw(yaw), variant));
            }
        }

        public float Scale(int tree, System.Random random)
        {
            var range = _climate.trees[tree].scale;
            return random.Range(range.x, range.y);
        }

        /// <summary>Scatters a per-hectare mix. <paramref name="inside"/> says whether a tree may grow at a point.</summary>
        public void Scatter(IReadOnlyList<MapTreeDensity> mix, float hectares, float spacing, System.Random random,
            Func<Vector2> candidate, Func<Vector2, bool> inside)
        {
            // MapAssetCheck rejects maxEdgeDistance on every mix scattered here.
            foreach (var density in mix)
            {
                Scatter(density, hectares, spacing, random, candidate, inside);
            }
        }

        /// <summary>One tree of a mix. The caller keeps <paramref name="inside"/> to its edge band.</summary>
        public void Scatter(MapTreeDensity density, float hectares, float spacing, System.Random random,
            Func<Vector2> candidate, Func<Vector2, bool> inside)
        {
            var wanted = Mathf.RoundToInt(density.perHectare * hectares);
            Scatter(_climate.TreeIndex(density.tree), wanted, spacing, random, candidate, inside);
        }

        private void Scatter(int tree, int wanted, float spacing, System.Random random, Func<Vector2> candidate,
            Func<Vector2, bool> inside)
        {
            var planted = 0;
            var misses = 0;
            while (planted < wanted && misses < wanted * ScatterAttempts + ScatterAttempts)
            {
                var point = candidate();
                if (!inside(point) || !Fits(point, spacing))
                {
                    misses++;
                    continue;
                }

                Plant(tree, point, random);
                planted++;
            }
        }
    }
}
