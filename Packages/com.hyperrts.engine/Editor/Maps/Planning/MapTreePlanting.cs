using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>Plants trees at authored-sector points in every symmetry copy, clear of roads, water, steep ground and placements.</summary>
    public sealed class MapTreePlanting
    {
        private const int ScatterAttempts = 6;
        private const float EntityFootprint = 1.5f;
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

        /// <summary>
        /// Makes every planted tree inside <paramref name="playArea"/> whose species has entity versions a placement,
        /// and returns the rest for the terrain: the backdrop and species without one.
        /// </summary>
        public List<MapTree> PlaceEntities(MapPlacements placements, Rect playArea)
        {
            var terrain = new List<MapTree>();
            var entries = new Dictionary<(int, int), MapProp>();
            foreach (var tree in Planted)
            {
                var species = _climate.trees[tree.Species];
                if (!species.HasEntities || !playArea.Contains(tree.Position))
                {
                    terrain.Add(tree);
                    continue;
                }

                if (!entries.TryGetValue((tree.Species, tree.Variant), out var entry))
                {
                    entries[(tree.Species, tree.Variant)] = entry = new MapProp
                    {
                        key = species.key, prefab = species.destructibles[tree.Variant], size = Vector2.one * EntityFootprint,
                    };
                }

                // Trees were spaced when planted, so they skip the clearance check.
                placements.All.Add(new MapPlacement(entry, tree.Position, tree.Yaw, 1f) { Scale = tree.Scale });
            }

            return terrain;
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
            var wanted = random.Count(density.perHectare * hectares);
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
