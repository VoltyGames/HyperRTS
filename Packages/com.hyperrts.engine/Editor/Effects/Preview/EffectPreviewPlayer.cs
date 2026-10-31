using System;
using System.Collections.Generic;
using System.IO;
using HyperRTS.Presentation.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>
    /// Plays one effect rig the way the pool does, in edit mode: a hidden instance in a scene, each graph bursting at
    /// the pivot with full density, streams running back and forth along a short path, fog clear. The Effect Preview
    /// window runs it with the editor's frames; the capture pauses it and steps it by hand.
    /// </summary>
    public sealed class EffectPreviewPlayer : IDisposable
    {
        public const float StepSeconds = 1f / 30f;
        public const float PathLength = 4f;
        public const float PathSpeed = 2f;

        private readonly Scene _scene;
        private readonly EffectPreviewEnvironment _environment = new();
        private GameObject _root;
        private EffectRig _rig;
        private GraphStream _stream;
        private VisualEffect[] _graphs = Array.Empty<VisualEffect>();
        private float _pathTime;

        public EffectPreviewPlayer(Scene scene) => _scene = scene;

        /// <summary>Where the rig plays; a stream's path is centred on it.</summary>
        public Vector3 Pivot { get; set; }

        public float Scale { get; set; } = 1f;

        public bool Paused { get; set; }

        public bool HasRig => _rig != null;

        /// <summary>Wind blowing towards a heading in degrees from +Z, at a strength in the units the graphs read.</summary>
        public void SetWind(float degrees, float strength) => _environment.SetWind(degrees, strength);

        /// <summary>Replaces the playing instance with one of <paramref name="prefab"/>; false when it has no rig.</summary>
        public bool Load(GameObject prefab)
        {
            Unload();
            if (prefab == null || !prefab.TryGetComponent<EffectRig>(out _))
            {
                return false;
            }

            _root = Object.Instantiate(prefab);
            _root.name = prefab.name;
            SceneManager.MoveGameObjectToScene(_root, _scene);
            foreach (var part in _root.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.hideFlags = HideFlags.DontSave;
            }

            _rig = _root.GetComponent<EffectRig>();
            _rig.Prepare();
            RefreshGraphs();
            return true;
        }

        /// <summary>
        /// Bursts every graph at the pivot and starts the stream. A restart clears the particles still alive first;
        /// a loop leaves them, as the pool does when it bursts a rig again.
        /// </summary>
        public void Play(bool restart)
        {
            if (!HasRig)
            {
                return;
            }

            EndStream();
            if (restart)
            {
                foreach (var effect in _graphs)
                {
                    effect.Reinit();
                }
            }

            _rig.Burst(Pivot, Quaternion.identity, Scale, 1f);
            _stream = _rig.StartStream();
            RefreshGraphs();
            _pathTime = 0f;
            MoveStream(0f);
        }

        /// <summary>Moves the stream and sends events waiting for a graph's first run; the editor runs the graphs.</summary>
        public void Step(float deltaTime)
        {
            if (!HasRig)
            {
                return;
            }

            foreach (var effect in _graphs)
            {
                effect.pause = Paused;
            }

            if (!Paused)
            {
                _pathTime += deltaTime;
                MoveStream(deltaTime);
            }

            _rig.Tick();
        }

        /// <summary>Whether every graph is culled, which keeps it from running until a camera sees it.</summary>
        public bool AllCulled => Array.TrueForAll(_graphs, effect => effect.culled);

        /// <summary>Advances the paused graphs by <paramref name="seconds"/> at the next editor frame, moving the stream along.</summary>
        public void Simulate(float seconds)
        {
            var steps = (uint)Mathf.Max(1, Mathf.RoundToInt(seconds / StepSeconds));
            _pathTime += seconds;
            MoveStream(seconds);
            foreach (var effect in _graphs)
            {
                effect.pause = true;
                effect.Simulate(StepSeconds, steps);
            }
        }

        /// <summary>The recipe behind each graph of the rig, found beside its VFX asset; graphs with none are left out.</summary>
        public IEnumerable<string> Recipes()
        {
            foreach (var effect in _graphs)
            {
                var graph = AssetDatabase.GetAssetPath(effect.visualEffectAsset);
                if (string.IsNullOrEmpty(graph))
                {
                    continue;
                }

                var recipe = Path.ChangeExtension(graph, null) + EffectGraphSpec.Extension;
                if (File.Exists(recipe))
                {
                    yield return recipe;
                }
            }
        }

        public void Dispose()
        {
            Unload();
            _environment.Dispose();
        }

        // Includes the copies a stream spawned beside the rig's own graphs.
        private void RefreshGraphs() => _graphs = _root.GetComponentsInChildren<VisualEffect>(true);

        private void Unload()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            _root = null;
            _rig = null;
            _stream = null;
            _graphs = Array.Empty<VisualEffect>();
        }

        private void EndStream()
        {
            if (_stream != null)
            {
                _rig.StopStream(_stream);
                _stream = null;
            }
        }

        private void MoveStream(float deltaTime)
        {
            if (_stream != null)
            {
                _stream.Move(PathPoint(_pathTime - deltaTime), PathPoint(_pathTime), Quaternion.identity, deltaTime, 1f, Scale);
            }
        }

        private Vector3 PathPoint(float time) =>
            Pivot + Vector3.right * (Mathf.PingPong(time * PathSpeed, PathLength) - PathLength * 0.5f);
    }
}
