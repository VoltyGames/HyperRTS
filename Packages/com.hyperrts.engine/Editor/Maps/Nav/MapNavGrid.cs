using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Fields;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Sectors;
using HyperRTS.Simulation.Navigation.Heights;
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

        private MapNavGrid(NavGrid grid, TerrainHeight terrain)
        {
            grid.Classes.Register(ToolsClass);
            grid.BuildAccess();
            Grid = grid;
            _terrain = terrain;
        }

        public NavGrid Grid { get; private set; }

        /// <summary>The grid as <see cref="ToolsClass"/> sees it.</summary>
        public NavAccess Access => new(Grid, ToolsClass, 0f);

        public static MapNavGrid Build(Scene entities, TerrainData terrainData, Vector3 terrainOrigin, Rect bounds,
            float waterLevel, MapRules rules)
        {
            var terrain = TerrainHeightAuthoring.Sample(terrainData, terrainOrigin, Allocator.Persistent);
            var cellSize = rules.navCellSize;
            var size = math.max(1, (int2)math.ceil(new float2(bounds.width, bounds.height) / cellSize));
            var cellGrid = new CellGrid(new float2(bounds.xMin, bounds.yMin), cellSize, size);
            var grid = NavGrid.Create(cellGrid, terrain, waterLevel);

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

        public float3 Center(int2 cell) => Grid.Grid.CenterPoint(cell);

        public int2 Cell(Vector2 point) => Grid.Grid.Cell(new float2(point.x, point.y));

        /// <summary>The walkable cell nearest a point, searching up to <paramref name="radius"/> metres out.</summary>
        public bool TryFindWalkable(Vector2 point, float radius, out int2 cell) =>
            Access.TryFindNearest(Cell(point), Mathf.CeilToInt(radius / Grid.Grid.CellSize), out cell);

        /// <summary>
        /// Whether any cell within <paramref name="radius"/> metres of a point is walkable and in <paramref name="reachable"/>
        /// (a flood). Not just the nearest cell: a walled building's inside is walkable but shut.
        /// </summary>
        public bool AnyReachable(Vector2 point, float radius, bool[] reachable)
        {
            var centre = Cell(point);
            var reach = Mathf.CeilToInt(radius / Grid.Grid.CellSize);
            for (var y = -reach; y <= reach; y++)
            {
                for (var x = -reach; x <= reach; x++)
                {
                    var cell = centre + new int2(x, y);
                    if (math.length(new float2(x, y)) > reach || !Grid.Grid.Contains(cell))
                    {
                        continue;
                    }

                    if (IsWalkable(cell) && reachable[Grid.Grid.Index(cell)])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Metres to walk from <paramref name="start"/> to each cell by index; infinity where unreachable.</summary>
        public float[] WalkDistances(int2 start)
        {
            var field = new FlowField(NavCorridor.Whole(Grid.Field.Layout, Allocator.Persistent), start,
                Allocator.Persistent);
            try
            {
                field.Build(Access);
                var distance = new float[Grid.Cells.Length];
                for (var i = 0; i < distance.Length; i++)
                {
                    distance[i] = field.Cost(Grid.Grid.Cell(i)) * Grid.Grid.CellSize;
                }

                return distance;
            }
            finally
            {
                field.Dispose();
            }
        }

        /// <summary>A copy with its own cells and access, for searches that close cells; the caller disposes it.</summary>
        internal NavGrid Copy()
        {
            var copy = NavGrid.Create(Grid.Grid, Grid.Terrain, Grid.WaterLevel);
            copy.Cells.CopyFrom(Grid.Cells);
            copy.Scenery.CopyFrom(Grid.Scenery);
            copy.Classes.Register(ToolsClass);
            copy.BuildAccess();
            return copy;
        }

        public void Dispose()
        {
            Grid.Dispose();
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
            foreach (var building in roots.SelectMany(r => r.GetComponentsInChildren<BuildingAuthoring>(true)))
            {
                if (!building.TryGetComponent<SectionsAuthoring>(out var sections))
                {
                    boxes.Add((building.transform, building.footprint));
                    continue;
                }

                // A sectioned building closes only what its sections do, as at runtime: a bridge's deck stays open.
                var materials = new List<SectionMaterial>();
                var shapes = SectionShape.Bake(sections.sections,
                    building.GetComponent<GameEntityAuthoring>().maxHealth, materials);
                foreach (var part in SectionsSetup.ObstacleParts(shapes).Where(part => part.Closed))
                {
                    var center = building.transform.TransformPoint(new Vector3(part.Center.x, 0f, part.Center.y));
                    grid.StampArea(new NavBox(center, building.transform.rotation, part.Size), NavAreaKind.Blocked, 0f);
                }
            }

            foreach (var (transform, size) in boxes)
            {
                grid.StampArea(new NavBox(transform.position, transform.rotation, size), NavAreaKind.Blocked, 0f);
            }
        }
    }
}
