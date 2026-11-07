using System;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>
    /// Plays the Effects page's rig at the Scene view pivot every editor update, replaying it on the remembered loop
    /// and settings, and follows the active scene. Disposing it stops the playback and puts the wind and fog globals back.
    /// </summary>
    internal sealed class EffectPlayback : IDisposable
    {
        private const float LongestFrame = 0.1f;

        private EffectPreviewPlayer _player = new(SceneManager.GetActiveScene());
        private GameObject _prefab;
        private double _lastTime = EditorApplication.timeSinceStartup;

        public EffectPlayback()
        {
            ApplySettings();
            EditorApplication.update += Tick;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
        }

        private static EffectPreviewSettings Settings => EditorState.instance.effectPreview;

        public bool Paused
        {
            set => _player.Paused = value;
        }

        /// <summary>Plays <paramref name="prefab"/> from the start; false when it is null or has no rig.</summary>
        public bool Load(GameObject prefab)
        {
            _prefab = prefab;
            var loaded = _player.Load(prefab);
            if (loaded)
            {
                Replay(true);
            }

            return loaded;
        }

        /// <summary>Bursts the rig again at the pivot. A restart clears the particles still alive first; a loop leaves them.</summary>
        public void Replay(bool restart)
        {
            var view = SceneView.lastActiveSceneView;
            _player.Pivot = view != null ? view.pivot : Vector3.zero;
            _player.Play(restart);
        }

        /// <summary>Reads the scale and wind from the remembered settings.</summary>
        public void ApplySettings()
        {
            _player.Scale = Settings.scale;
            _player.SetWind(Settings.windHeading, Settings.windStrength);
        }

        public void Dispose()
        {
            EditorApplication.update -= Tick;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            _player.Dispose();
        }

        // The playing instance lives in the scene, so a new scene gets a new player and instance.
        private void OnSceneChanged(Scene previous, Scene current)
        {
            var paused = _player.Paused;
            _player.Dispose();
            _player = new EffectPreviewPlayer(current) { Paused = paused };
            ApplySettings();
            Load(_prefab);
        }

        private void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Min((float)(now - _lastTime), LongestFrame);
            _lastTime = now;
            if (!_player.HasRig)
            {
                return;
            }

            if (Settings.loop && _player.SincePlay >= Settings.interval)
            {
                Replay(false);
            }

            _player.Step(deltaTime);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}
