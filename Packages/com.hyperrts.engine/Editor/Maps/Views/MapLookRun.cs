using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.PlayMode;
using HyperRTS.Input.Cameras;
using HyperRTS.Presentation.Environment.Weather;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>One run's shots and where it is in them.</summary>
    internal sealed class MapLookRun
    {
        private const int Width = 1920;
        private const int Height = 1080;

        // Frames the world gets to load and the start kit to settle before the first shot.
        private const int WarmupFrames = 240;

        // Frames a view renders before it is captured, so Surface Cache GI, the grass scatter and LODs settle.
        private const int SettleFrames = 90;

        private readonly MapSpec _spec;
        private readonly List<string> _weathers;
        private readonly int _quality;
        private readonly int _previousQuality;
        private readonly List<(string weather, MapLookSpec look, float zoom)> _plan = new();
        private int _next;
        private int _readyFrame = -1;
        private int _startFrame = -1;
        private CameraController _controller;

        public MapLookRun(MapSpec spec, List<string> weathers, int quality)
        {
            _spec = spec;
            _weathers = weathers;
            _quality = quality;
            _previousQuality = QualitySettings.GetQualityLevel();
            Summary = new MapLookSummary { map = spec.name, quality = QualitySettings.names[quality], width = Width, height = Height };
        }

        public MapLookSummary Summary { get; }

        public string Progress => $"{_next} of {_plan.Count} shots of '{_spec.name}'";

        /// <summary>Advances the run by a frame; true when every shot is written.</summary>
        public bool Step()
        {
            if (_controller == null && !Begin())
            {
                return false;
            }

            if (Time.frameCount < _readyFrame)
            {
                return false;
            }

            if (_readyFrame >= 0)
            {
                Shoot(_plan[_next]);
                _next++;
            }

            if (_next == _plan.Count)
            {
                return true;
            }

            Frame(_plan[_next]);
            _readyFrame = Time.frameCount + SettleFrames;
            return false;
        }

        // Waits for the match camera and weathers, then plans every shot.
        private bool Begin()
        {
            if (_startFrame < 0)
            {
                _startFrame = Time.frameCount;
            }

            var camera = Camera.main;
            var weatherSet = UnityEngine.Object.FindAnyObjectByType<WeatherSet>();
            if (camera == null || weatherSet == null || Time.frameCount < _startFrame + WarmupFrames
                || !camera.TryGetComponent(out CameraController controller))
            {
                return false;
            }

            QualitySettings.SetQualityLevel(_quality, true);
            var keys = _weathers.Count > 0 ? _weathers : weatherSet.atmospheres.Keys.OrderBy(k => k).ToList();
            var zooms = new[] { controller.minZoom, (controller.minZoom + controller.maxZoom) * 0.5f, controller.maxZoom };
            foreach (var weather in keys)
            {
                // Throws naming an unknown key before any shot is taken.
                weatherSet.Find(weather);
                foreach (var look in _spec.looks)
                {
                    _plan.AddRange(zooms.Select(zoom => (weather, look, zoom)));
                }
            }

            // The capture places the camera itself; the controller would pan it with the pointer.
            controller.enabled = false;
            _controller = controller;
            return true;
        }

        private void Frame((string weather, MapLookSpec look, float zoom) shot)
        {
            UnityEngine.Object.FindAnyObjectByType<WeatherSet>().Apply(shot.weather);
            if (PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                Cheats.SetFog(entityManager, shot.look.fog);
            }

            var view = new MapView(shot.look.name, shot.look.pos.ToVector2(), shot.zoom, shot.look.yaw);
            MapViews.Aim(Camera.main, view, _controller);
        }

        private void Shoot((string weather, MapLookSpec look, float zoom) shot)
        {
            var folder = Path.Combine(MapLookCapture.Folder(_spec.name), shot.weather);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{shot.look.name}-{Mathf.RoundToInt(shot.zoom)}m.png");
            EditorTextures.RenderPng(Camera.main, Width, Height, path, settledGI: true);
            Summary.shots.Add(new MapLookShot { weather = shot.weather, point = shot.look.name, zoom = shot.zoom, path = path });
        }

        /// <summary>Puts the camera and quality back and writes the summary; returns its path.</summary>
        public string End(string failure)
        {
            if (_controller != null)
            {
                _controller.enabled = true;
            }

            QualitySettings.SetQualityLevel(_previousQuality, true);
            Summary.failure = failure;
            var folder = MapLookCapture.Folder(_spec.name);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "summary.json");
            File.WriteAllText(path, JsonUtility.ToJson(Summary, true));
            return path;
        }
    }
}
