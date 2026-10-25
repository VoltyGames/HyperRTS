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
    /// The nav grid a built map gives the simulation, made in the editor with the runtime's stamps: the baked terrain
    /// classified, then the SubScene's nav areas, obstacles and building footprints stamped on top. Tools read it
    /// as one <see cref="ToolsClass"/> with no wall margin, so rush times stay geometric.
    /// </summary>
    public sealed class MapNavGrid : IDisposable
    {
        /// <summary>A small ground unit that walks through all scenery: the map's own walkable ground.</summary>
        public static readonly MovementClass ToolsClass = new(NavLayer.Ground, NavAccess.AllScenery, 0.5f);

        private TerrainHeight _terrain;
        private GridAStar _search;

        private MapNavGrid(NavGrid grid, TerrainHeight terrain)
        {
            grid.Classes.Register(ToolsClass);
            grid.BuildAccess();
            Grid = grid;
            _terrain = terrain;
            _search = new GridAStar(grid.Cells.Length, Allocator.Persistent);
        }

        public NavGrid Grid { get; private set; }

        /// <summary>The grid as <see cref="ToolsClass"/> sees it.</summary>
        public NavAccess Access => new(Grid, ToolsClass, 0f);

        /// <summary>One A* reused by every path search on this grid and its corridor-closed copies.</summary>
        internal ref GridAStar Search => ref _search;

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
                grid.Cells[i] = (byte)grid.GroundSurface(i, true, gradient);
            }

            var roots = entities.GetRootGameObjects();
            StampAreas(grid, roots);
            StampObstacles(grid, roots);
            return new MapNavGrid(grid, terrain);
        }

        /// <summary>
        /// A nav grid made by hand (tests, tools) rather than from a scene, its cells filled; it owns
        /// <paramref name="grid"/> and builds its access.
        /// </summary>
        public static MapNavGrid Wrap(NavGrid grid) => new(grid, default);

        public bool IsWalkable(int2 cell) => Grid.IsWalkable(cell);

        public float3 Center(int2 cell) => Grid.CellCenter(cell);

        public int2 Cell(Vector2 point) => Grid.WorldToCell(new float2(point.x, point.y));

        /// <summary>The walkable cell nearest a point, searching up to <paramref name="radius"/> metres out.</summary>
        public bool TryFindWalkable(Vector2 point, float radius, out int2 cell) =>
            Access.TryFindNearest(Cell(point), Mathf.CeilToInt(radius / Grid.CellSize), out cell);

        /// <summary>A copy with its own cells and access, for searches that close cells; the caller disposes it.</summary>
        internal NavGrid Copy()
        {
            var copy = NavGrid.Create(Grid.Size, Grid.Min, Grid.CellSize, Grid.Terrain, Grid.WaterLevel);
            copy.Cells.CopyFrom(Grid.Cells);
            copy.Scenery.CopyFrom(Grid.Scenery);
            copy.Classes.Register(ToolsClass);
            copy.BuildAccess();
            return copy;
        }

        public void Dispose()
        {
            Grid.Dispose();
            _search.Dispose();
            if (_terrain.IsCreated)
            {
                _terrain.Blob.Dispose();
            }
        }

        // Decks go last, so they span water areas, as NavGridSystem stamps them.
        private static void StampAreas(NavGrid grid, GameObject[] roots)
        {
            var areas = roots.SelectMany(r => r.GetComponentsInChildren<NavAreaAuthoring>(true));
            foreach (var area in areas.OrderBy(a => a.kind == NavAreaKind.Deck))
            {
                var transform = area.transform;
                grid.StampArea(new NavBox(transform.position, transform.rotation, area.size), area.kind,
                    transform.position.y);
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
                grid.StampArea(new NavBox(transform.position, transform.rotation, size), NavAreaKind.Blocked, 0f);
            }
        }
    }
}
