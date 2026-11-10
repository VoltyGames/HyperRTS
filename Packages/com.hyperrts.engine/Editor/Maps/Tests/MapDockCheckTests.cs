using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Nav;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Docks;
using HyperRTS.Simulation.Navigation.Grid;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests
{
    /// <summary>
    /// Two 5 m docks on open 1 m ground, each with one bay 10 m north of its centre facing east, checked with a
    /// 10.3 m hull.
    /// </summary>
    public class MapDockCheckTests
    {
        private static readonly Vector2 Size = new(4.9f, 5f);

        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown() => _objects.ForEach(Object.DestroyImmediate);

        [TestCase(4f, 10f, 1, TestName = "ADockWhoseBayHitsAnother_IsAnErrorNamingIt")]
        [TestCase(0f, 30f, 0, TestName = "DocksSpacedApart_PassTheCheck")]
        public void TheDockingHull_MustFitEveryBay(float x, float z, int errors)
        {
            var docks = new[] { Dock("Field A", Vector3.zero), Dock("Field B", new Vector3(x, 0f, z)) };
            var grid = NavGrid.Create(new CellGrid(new float2(-30f, -20f), 1f, new int2(60, 70)), default, 0f);
            for (var i = 0; i < grid.Cells.Length; i++)
            {
                grid.Cells[i] = (byte)NavSurface.Land;
            }

            foreach (var dock in docks)
            {
                grid.StampArea(new NavBox(dock.transform.position, quaternion.identity, Size), NavAreaKind.Blocked, 0f);
            }

            using var nav = MapNavGrid.Wrap(grid);
            var issues = new MapIssues();
            MapDockCheck.Check(docks, nav, new MapDockingHull(10.3f, 1.27f, "Truck"), issues);

            Assert.That(issues.Errors.Count, Is.EqualTo(errors), issues.ToString());
            Assert.That(issues.Errors.All(e => e.ToString().Contains("'Field A'") && e.ToString().Contains("'Field B'")));
        }

        private DockAuthoring Dock(string name, Vector3 position)
        {
            var root = new GameObject(name);
            _objects.Add(root);
            root.transform.position = position;
            root.AddComponent<NavObstacleAuthoring>().size = Size;
            var stop = new GameObject("Bay_01").transform;
            stop.SetParent(root.transform, false);
            stop.SetLocalPositionAndRotation(new Vector3(0f, 0f, 10f), Quaternion.Euler(0f, 90f, 0f));
            var dock = root.AddComponent<DockAuthoring>();
            dock.bays = new[] { new DockAuthoring.Bay { stop = stop, runIn = DockAuthoring.DefaultRunIn } };
            return dock;
        }
    }
}
