using System.Collections.Generic;
using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Report;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>
    /// Standard screenshots of the open map from the match camera: the first bases, every crossing and settlement of the authored sector
    /// (wide and close), and an overview, into <c>Logs/MapReports/&lt;Map&gt;/</c>. Agents and reviewers look at
    /// these instead of flying a camera by hand.
    /// </summary>
    public static class MapViews
    {
        /// <summary>The view that shows the whole map; the build keeps it as the map's picture.</summary>
        public const string OverviewName = "overview";

        private const int Width = 1600;
        private const int Height = 900;
        private const int Bases = 2;

        /// <summary>The views a layout gets: bases, crossings, settlements, the centre, an overview.</summary>
        public static List<MapView> For(MapLayout layout, MapShapes shapes, MapRules rules)
        {
            var views = new List<MapView> { new("centre", Vector2.zero, rules.viewHeight) };
            for (var i = 0; i < Mathf.Min(Bases, layout.Spawns.Count); i++)
            {
                var spawn = layout.Spawns[i];
                views.Add(new MapView($"base-{spawn.Slot}", spawn.Position, rules.viewHeight, spawn.Yaw));
            }

            var crossings = shapes.Crossings.FindAll(c => layout.Symmetry.InSector(c.Position));
            for (var i = 0; i < crossings.Count; i++)
            {
                views.Add(new MapView($"crossing-{i + 1}", crossings[i].Position, rules.viewHeight));
            }

            foreach (var settlement in layout.Settlements)
            {
                if (settlement.Copy == 0)
                {
                    var center = settlement.Placed.center.ToVector2();
                    views.Add(new MapView($"settlement-{settlement.Index + 1}", center, rules.viewHeight));
                    views.Add(new MapView($"settlement-{settlement.Index + 1}-close", center, rules.viewCloseHeight));
                }
            }

            views.Add(new MapView(OverviewName, Vector2.zero, layout.Size.x * 0.45f));
            return views;
        }

        /// <summary>
        /// Renders the views of the open scene to PNGs with the scene's own match camera (so post-processing matches
        /// the game), puts the camera back, and returns the paths.
        /// </summary>
        public static List<string> Capture(string map, IEnumerable<MapView> views, MapRules rules)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                throw new System.InvalidOperationException($"Map '{map}' has no main camera to capture its views with.");
            }

            MapSubScenes.Load();
            var folder = MapReport.ViewsFolder(map);
            Directory.CreateDirectory(folder);
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var paths = new List<string>();
            var transform = camera.transform;
            var (position, rotation, fieldOfView) = (transform.position, transform.rotation, camera.fieldOfView);
            var mask = camera.overrideSceneCullingMask;
            try
            {
                // Entities streamed into the editor world carry the editor's scene mask, which a game camera skips.
                camera.overrideSceneCullingMask = ulong.MaxValue;
                camera.fieldOfView = rules.viewFieldOfView;
                foreach (var view in views)
                {
                    Aim(camera, view, rules);
                    var path = Path.Combine(folder, view.Name + ".png");
                    EditorTextures.RenderPng(camera, target, path);
                    paths.Add(path);
                }
            }
            finally
            {
                transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fieldOfView;
                camera.overrideSceneCullingMask = mask;
                Object.DestroyImmediate(target);
            }

            return paths;
        }

        /// <summary>Looks down at the view's focus from its height, at the match camera's pitch.</summary>
        private static void Aim(Camera camera, MapView view, MapRules rules)
        {
            var rotation = Quaternion.Euler(rules.viewPitch, view.Yaw, 0f);
            var distance = view.Height / Mathf.Sin(rules.viewPitch * Mathf.Deg2Rad);
            var focus = new Vector3(view.Focus.x, 0f, view.Focus.y);
            camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
