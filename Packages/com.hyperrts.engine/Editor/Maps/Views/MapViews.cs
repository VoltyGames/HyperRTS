using System.Collections.Generic;
using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Input.Cameras;
using UnityEditor.SceneManagement;
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
        public static List<string> Capture(string map, IEnumerable<MapView> views)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                throw new System.InvalidOperationException($"Map '{map}' has no main camera to capture its views with.");
            }

            if (!camera.TryGetComponent(out CameraController controller))
            {
                throw new System.InvalidOperationException($"Map '{map}' main camera has no CameraController to pitch its views.");
            }

            MapSubScenes.Load();
            var folder = MapReport.ViewsFolder(map);
            Directory.CreateDirectory(folder);
            var paths = new List<string>();
            var transform = camera.transform;
            var (position, rotation) = (transform.position, transform.rotation);
            var mask = camera.overrideSceneCullingMask;
            try
            {
                // The SubScene streaming system stamps editor-world entities with the default editor scene mask.
                camera.overrideSceneCullingMask = EditorSceneManager.DefaultSceneCullingMask;
                foreach (var view in views)
                {
                    Aim(camera, view, controller);
                    var path = Path.Combine(folder, view.Name + ".png");
                    EditorTextures.RenderPng(camera, Width, Height, path);
                    paths.Add(path);
                }
            }
            finally
            {
                transform.SetPositionAndRotation(position, rotation);
                camera.overrideSceneCullingMask = mask;
            }

            return paths;
        }

        /// <summary>Looks down at the view's focus from its height, at the pitch the match camera has there.</summary>
        private static void Aim(Camera camera, MapView view, CameraController controller)
        {
            var pitch = CameraController.PitchAt(view.Height, controller.minZoom, controller.maxZoom,
                controller.pitchAtMinZoom, controller.pitchAtMaxZoom);
            var rotation = Quaternion.Euler(pitch, view.Yaw, 0f);
            var distance = view.Height / Mathf.Sin(pitch * Mathf.Deg2Rad);
            var focus = new Vector3(view.Focus.x, 0f, view.Focus.y);
            camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
