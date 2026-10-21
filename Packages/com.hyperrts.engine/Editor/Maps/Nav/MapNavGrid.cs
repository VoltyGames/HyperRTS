using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Maps.Nav
{
    /// <summary>
    /// The nav grid a built map gives the simulation, made in the editor: the baked terrain classified like
    /// <c>NavGridSystem</c>, then the SubScene's nav areas, obstacles and building footprints stamped on top.
    /// </summary>
    public sealed class MapNavGrid : IDisposable
    {
        private TerrainHeight _terrain;

        private MapNavGrid(NavGrid grid, TerrainHeight terrain)
        {
            Grid = grid;
            _terrain = terrain;
        }

        public NavGrid Grid { get; private set; }

        public static MapNavGrid Build(Scene entities, TerrainData terrainData, Vector3 terrainOrigin, Rect bounds,
            float waterLevel, MapRules rules)
        {
            var terrain = TerrainHeightAuthoring.Sample(terrainData, terrainOrigin, Allocator.Persistent);
            var cellSize = rules.navCellSize;
            var size = math.max(1, (int2)math.ceil(new float2(bounds.width, bounds.height) / cellSize));
            var grid = NavGrid.Create(size, new float2(bounds.xMin, bounds.yMin), cellSize, terrain, waterLevel);

            var gradient = NavGround.MaxGradient(rules.maxSlope);
            for (var i = 0; i < grid.Cells.Length; i++)
            {
                var center = grid.CellCenter(grid.Cell(i)).xz;
                grid.Cells[i] = (byte)NavGround.Classify(terrain, center, cellSize, waterLevel, true, gradient);
            }

            var roots = entities.GetRootGameObjects();
            StampAreas(grid, roots);
            StampObstacles(grid, roots);
            grid.BuildClearance();
            return new MapNavGrid(grid, terrain);
        }

        /// <summary>A nav grid made by hand (tests, tools) rather than from a scene; it owns <paramref name="grid"/>.</summary>
        public static MapNavGrid Wrap(NavGrid grid) => new(grid, default);

        public bool IsWalkable(int2 cell) => Grid.IsWalkable(cell);

        public float3 Center(int2 cell) => Grid.CellCenter(cell);

        public int2 Cell(Vector2 point) => Grid.WorldToCell(new float2(point.x, point.y));

        /// <summary>The walkable cell nearest a point, searching up to <paramref name="radius"/> metres out.</summary>
        public bool TryFindWalkable(Vector2 point, float radius, out int2 cell) =>
            Grid.TryFindNearestWalkable(Cell(point), Mathf.CeilToInt(radius / Grid.CellSize), out cell);

        public void Dispose()
        {
            Grid.Dispose();
            if (_terrain.IsCreated)
            {
                _terrain.Blob.Dispose();
            }
        }

        private static void StampAreas(NavGrid grid, GameObject[] roots)
        {
            var areas = roots.SelectMany(r => r.GetComponentsInChildren<NavAreaAuthoring>(true)).ToList();
            foreach (var decks in new[] { false, true })
            {
                foreach (var area in areas.Where(a => (a.kind == NavAreaKind.Deck) == decks))
                {
                    var transform = area.transform;
                    Stamp(grid, transform, area.size, index =>
                    {
                        grid.Cells[index] = (byte)NavGround.Stamp(area.kind, (NavSurface)grid.Cells[index]);
                        if (decks)
                        {
                            grid.DeckHeights[index] = transform.position.y;
                        }
                    });
                }
            }
        }

        private static void StampObstacles(NavGrid grid, GameObject[] roots)
        {
            var boxes = new List<(Transform Transform, Vector2 Size)>();
            boxes.AddRange(roots.SelectMany(r => r.GetComponentsInChildren<NavObstacleAuthoring>(true))
                .Select(o => (o.transform, o.size)));
            boxes.AddRange(roots.SelectMany(r => r.GetComponentsInChildren<BuildingAuthoring>(true))
                .Select(b => (b.transform, b.footprint)));
            foreach (var (transform, size) in boxes)
            {
                Stamp(grid, transform, size, index => grid.Cells[index] = (byte)NavSurface.Blocked);
            }
        }

        private static void Stamp(NavGrid grid, Transform transform, Vector2 size, Action<int> stamp)
        {
            var box = new NavBox(transform.position, transform.rotation, size);
            grid.GetBoxArea(box, out var min, out var max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (grid.InBox(box, new int2(x, y)))
                    {
                        stamp(grid.Index(new int2(x, y)));
                    }
                }
            }
        }
    }
}
