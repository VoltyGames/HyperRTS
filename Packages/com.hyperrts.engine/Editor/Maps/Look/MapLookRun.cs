using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Views;
using HyperRTS.Editor.PlayMode;
using HyperRTS.Input.Cameras;
using HyperRTS.Presentation.Environment.Weather;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Look
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
        private readonly Material _water;
        private readonly List<string> _weathers;
        private readonly bool _graded;
        private readonly int _quality;
        private readonly bool _cost;
        private readonly int _previousQuality;
        private readonly List<(string weather, MapLookSpec look, float zoom)> _plan = new();
        private int _next;
        private int _readyFrame = -1;
        private int _startFrame = -1;
        private Camera _camera;
        private CameraController _controller;
        private WeatherSet _weatherSet;
        private Volume _ungraded;
        private RenderTexture _target;
        private MapLookCost _costProbe;

        public MapLookRun(MapSpec spec, Material water, List<string> weathers, bool graded, int quality, bool cost)
        {
            _spec = spec;
            _water = water;
            _weathers = weathers;
            _graded = graded;
            _quality = quality;
            _cost = cost;
            _previousQuality = QualitySettings.GetQualityLevel();
            Summary = new MapLookSummary
            {
                map = spec.name, quality = QualitySettings.names[quality], graded = graded, width = Width, height = Height,
            };
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
                if (_costProbe == null)
                {
                    Shoot(_plan[_next]);
                }
                else if (!_costProbe.Measure(_plan[_next].look.name, _plan[_next].zoom))
                {
                    return false;
                }

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
            var weatherSet = Object.FindAnyObjectByType<WeatherSet>();
            if (camera == null || weatherSet == null || Time.frameCount < _startFrame + WarmupFrames
                || !camera.TryGetComponent(out CameraController controller))
            {
                return false;
            }

            QualitySettings.SetQualityLevel(_quality, true);
            MapLookQuality.Applied?.Invoke(_quality);
            var keys = _weathers.Count > 0 ? _weathers : weatherSet.atmospheres.Keys.OrderBy(k => k).ToList();
            if (_cost)
            {
                keys = keys.Take(1).ToList();
            }

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

            if (!_graded)
            {
                _ungraded = Ungraded();
            }

            // The capture places the camera itself; the controller would pan it with the pointer.
            controller.enabled = false;
            (_camera, _controller, _weatherSet) = (camera, controller, weatherSet);

            // The camera keeps rendering through its own frame loop, upscaler history and all, but into a texture
            // without the screen-space interface over it.
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = _target;
            if (_cost)
            {
                _costProbe = new MapLookCost(camera, _water, _spec.name, Summary.quality, keys[0]);
            }

            return true;
        }

        private void Frame((string weather, MapLookSpec look, float zoom) shot)
        {
            _weatherSet.Apply(shot.weather);
            if (PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                Cheats.SetFog(entityManager, shot.look.fog);
            }

            var view = new MapView(shot.look.name, shot.look.pos.ToVector2(), shot.zoom, shot.look.yaw);
            MapViews.Aim(_camera, view, _controller);
        }

        private void Shoot((string weather, MapLookSpec look, float zoom) shot)
        {
            var folder = Path.Combine(MapLookCapture.Folder(_spec.name, _graded), shot.weather);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{shot.look.name}-{Mathf.RoundToInt(shot.zoom)}m.png");
            EditorTextures.WritePng(_target, path);
            Summary.shots.Add(new MapLookShot { weather = shot.weather, point = shot.look.name, zoom = shot.zoom, path = path });
        }

        /// <summary>
        /// A global volume over every other that zeroes the weathers' Color Lookup, for captures the lookup fitter
        /// pairs with paintovers.
        /// </summary>
        private static Volume Ungraded()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<ColorLookup>(true).contribution.Override(0f);
            var volume = new GameObject("Look Capture Ungraded").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = float.MaxValue;
            volume.sharedProfile = profile;
            return volume;
        }

        /// <summary>Puts the camera and quality back and writes the summary; returns its path.</summary>
        public string End(string failure)
        {
            if (_controller != null)
            {
                _controller.enabled = true;
                _camera.targetTexture = null;
            }

            if (_target != null)
            {
                Object.Destroy(_target);
            }

            _costProbe?.Dispose();

            if (_ungraded != null)
            {
                Object.Destroy(_ungraded.sharedProfile);
                Object.Destroy(_ungraded.gameObject);
            }

            QualitySettings.SetQualityLevel(_previousQuality, true);
            Summary.failure = failure;
            var folder = MapLookCapture.Folder(_spec.name, _graded);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "summary.json");
            File.WriteAllText(path, JsonUtility.ToJson(Summary, true));
            if (_costProbe != null)
            {
                File.WriteAllText(Path.Combine(folder, "cost.json"), JsonUtility.ToJson(_costProbe.Summary, true));
            }

            return path;
        }
    }
}
