using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>
    /// HyperRTS ▸ Effect Preview: plays an effect rig at the Scene view pivot on a loop, the way the pool plays it,
    /// with replay, scale and wind controls. Saving a recipe of the rig rebuilds that graph and replays.
    /// </summary>
    public sealed class EffectPreviewWindow : EditorWindow
    {
        private const string WindowTitle = "Effect Preview";
        private const string PickHint = "Pick an effect rig prefab, or select one in the Project window.";
        private const float LongestFrame = 0.1f;

        [SerializeField] private GameObject _rig;

        private EffectPreviewPlayer _player;
        private Label _hint;
        private VisualElement _controls;
        private ObjectField _picker;
        private double _lastTime;
        private float _sinceBurst;

        [MenuItem(EditorMenu.EffectPreview, false, EditorMenu.EffectPreviewPriority)]
        public static void Open() => GetWindow<EffectPreviewWindow>(WindowTitle);

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.Layout(typeof(EffectPreviewWindow)).CloneTree(root);
            _hint = root.Require<Label>("hint");
            _controls = root.Require<VisualElement>("controls");
            _picker = root.Require<ObjectField>("rig");
            _picker.objectType = typeof(GameObject);
            _picker.SetValueWithoutNotify(_rig);
            _picker.RegisterValueChangedCallback(evt => Show(evt.newValue as GameObject));

            EditorState.Bind(root);
            root.Require<Toggle>("pause").RegisterValueChangedCallback(change => _player.Paused = change.newValue);
            root.Require<PropertyField>("scale").RegisterValueChangeCallback(_ => _player.Scale = Settings.scale);
            root.Require<PropertyField>("wind-strength").RegisterValueChangeCallback(_ => ApplyWind());
            root.Require<PropertyField>("wind-heading").RegisterValueChangeCallback(_ => ApplyWind());
            root.Require<Button>("replay").clicked += () => Replay(true);

            _player.Scale = Settings.scale;
            ApplyWind();
            Show(_rig);
        }

        public void OnEnable()
        {
            _player = new EffectPreviewPlayer(SceneManager.GetActiveScene());
            _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            Selection.selectionChanged += OnSelectionChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
        }

        public void OnDisable()
        {
            EditorApplication.update -= Tick;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            _player.Dispose();
        }

        /// <summary>Rebuilds the graphs whose recipe was imported, then plays the rig again.</summary>
        public void OnImported(string[] imported)
        {
            var changed = _player.Recipes().Where(recipe => Array.IndexOf(imported, recipe) >= 0).Distinct().ToList();
            if (changed.Count == 0)
            {
                return;
            }

            // After the import, since the build imports the graph it writes.
            EditorApplication.delayCall += () => Rebuild(changed);
        }

        private void Rebuild(List<string> recipes)
        {
            if (this == null)
            {
                return;
            }

            try
            {
                recipes.ForEach(EffectGraphBuilder.Build);
            }
            catch (Exception exception)
            {
                Say(exception.Message);
                return;
            }

            Show(_rig);
        }

        private void Show(GameObject rig)
        {
            _rig = rig;
            _picker.SetValueWithoutNotify(rig);
            var loaded = _player.Load(rig);
            _controls.SetHidden(!loaded);
            if (loaded)
            {
                Say(string.Empty);
                Replay(true);
            }
            else
            {
                Say(rig == null ? PickHint : $"{rig.name} has no Effect Rig.");
            }
        }

        private void Say(string message)
        {
            _hint.text = message;
            _hint.SetHidden(message.Length == 0);
        }

        private void Replay(bool restart)
        {
            var view = SceneView.lastActiveSceneView;
            _player.Pivot = view != null ? view.pivot : Vector3.zero;
            _player.Play(restart);
            _sinceBurst = 0f;
        }

        private static EffectPreviewSettings Settings => EditorState.instance.effectPreview;

        private void ApplyWind() => _player.SetWind(Settings.windHeading, Settings.windStrength);

        private void OnSelectionChanged()
        {
            if (Settings.follow && Selection.activeObject is GameObject selected && EditorUtility.IsPersistent(selected) && selected.TryGetComponent<EffectRig>(out _))
            {
                Show(selected);
            }
        }

        private void OnSceneChanged(Scene previous, Scene current)
        {
            _player.Dispose();
            _player = new EffectPreviewPlayer(current) { Scale = Settings.scale };
            ApplyWind();
            Show(_rig);
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

            if (!_player.Paused)
            {
                _sinceBurst += deltaTime;
            }

            if (Settings.loop && _sinceBurst >= Settings.interval)
            {
                Replay(false);
            }

            _player.Step(deltaTime);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}
