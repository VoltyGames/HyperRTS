using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Validation;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>
    /// Tools ▸ Effects: every effect rig prefab by kind. Picking one plays it at the Scene view pivot on a loop, the way
    /// the pool plays it, with its recipes to build and a frame capture. It plays only while the page is shown.
    /// </summary>
    public class EffectsPage : HubPage
    {
        private const long ReloadDelayMilliseconds = 500;
        private const string PickHint = "Pick an effect from the list.";
        private static readonly string[] PlaySettings = { "scale", "wind-strength", "wind-heading" };

        private List<EffectRigEntry> _rigs = new();
        private EffectRigEntry _current;
        private string _filter = "";
        private EffectPlayback _playback;
        private IVisualElementScheduledItem _reload;
        private VisualElement _root;
        private PageShell _shell;
        private EffectRigList _list;
        private Label _hint;
        private Label _title;
        private VisualElement _controls;
        private VisualElement _recipes;
        private Label _noRecipes;
        private Button _capture;
        private IssueList _issues;

        public override string Title => "Effects";

        public override string Glyph => "sparkles";

        public override int Order => 25;

        private static EffectPreviewSettings Settings => EditorState.instance.effectPreview;

        public override VisualElement Build()
        {
            _root = Layout().Instantiate();
            _shell = _root.Require<PageShell>("shell");
            _hint = _root.Require<Label>("hint");
            _title = _root.Require<Label>("title");
            _controls = _root.Require<VisualElement>("controls");
            _recipes = _root.Require<VisualElement>("recipes");
            _noRecipes = _root.Require<Label>("no-recipes");
            _capture = _root.Require<Button>("capture");
            _issues = _root.Require<IssueList>("issues");
            _list = new EffectRigList(_root.Require<TreeView>("list"), Layout("Row"));
            _list.Picked += Show;

            _root.Require<ToolbarSearchField>("search").RegisterValueChangedCallback(change =>
            {
                _filter = change.newValue;
                ShowList();
            });

            EditorState.Bind(_root);
            _root.Require<Toggle>("pause").RegisterValueChangedCallback(change => _playback.Paused = change.newValue);
            foreach (var setting in PlaySettings)
            {
                // The binding raises these on attach, before Subscribe has made the playback.
                _root.Require<PropertyField>(setting).RegisterValueChangeCallback(_ => _playback?.ApplySettings());
            }

            _root.Require<Button>("replay").clicked += () => _playback.Replay(true);
            _capture.clicked += () => _ = CaptureFrames();

            _root.WhileAttached(Subscribe, Unsubscribe);
            Reload();
            return _root;
        }

        private void Subscribe()
        {
            _playback = new EffectPlayback();
            EditorApplication.update += ShowRequested;
            EffectPreviewImport.EffectsChanged += QueueReload;
            Selection.selectionChanged += OnSelectionChanged;
            EffectPreviewImport.Imported += OnImported;
            Show(FindRig(Settings.lastEffect));
        }

        private void Unsubscribe()
        {
            EditorApplication.update -= ShowRequested;
            EffectPreviewImport.EffectsChanged -= QueueReload;
            Selection.selectionChanged -= OnSelectionChanged;
            EffectPreviewImport.Imported -= OnImported;
            _playback.Dispose();
            _playback = null;
            _reload?.Pause();
        }

        // An import raises many changes, so the list reloads once they have stopped for a moment.
        private void QueueReload() => (_reload ??= _root.schedule.Execute(Reload)).ExecuteLater(ReloadDelayMilliseconds);

        private void Reload()
        {
            _rigs = EffectRigEntry.Find();
            _current = FindRig(_current?.Path);
            _shell.ShowEmpty(_rigs.Count == 0);
            ShowList();
        }

        private void ShowList() =>
            _list.Show(_rigs.Where(rig => rig.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)), _current);

        private EffectRigEntry FindRig(string path) => _rigs.FirstOrDefault(rig => rig.Path == path);

        private void Show(EffectRigEntry rig)
        {
            _current = rig;
            var prefab = rig == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(rig.Path);
            var loaded = _playback.Load(prefab);
            _controls.SetHidden(!loaded);
            Say(loaded ? string.Empty : PickHint);
            if (!loaded)
            {
                return;
            }

            Settings.lastEffect = rig.Path;
            EditorState.instance.Save();
            _title.text = $"{rig.Name} ({rig.Kind})";
            ShowRecipes(rig.Recipes);
            var problems = EffectRig.Problems(prefab)
                .Select(problem => new ValidationIssue { Severity = MessageType.Error, Message = problem,
                    Context = prefab })
                .ToList();
            _issues.Show(problems);
            _issues.SetHidden(problems.Count == 0);
        }

        // Selects and shows a rig asked for outside the list, unless it is already shown.
        private void Focus(EffectRigEntry rig)
        {
            if (rig != null && rig.Path != _current?.Path)
            {
                _list.Select(rig);
                Show(rig);
            }
        }

        // The recipe inspector's Preview button asks for an effect by setting the remembered one.
        private void ShowRequested()
        {
            if (Settings.lastEffect != _current?.Path)
            {
                Focus(FindRig(Settings.lastEffect));
            }
        }

        private void OnSelectionChanged()
        {
            if (Settings.follow && Selection.activeObject != null)
            {
                Focus(FollowTarget(Selection.activeObject));
            }
        }

        // A recipe stays on the current effect when that effect uses it.
        private EffectRigEntry FollowTarget(Object selected)
        {
            var path = AssetDatabase.GetAssetPath(selected);
            if (selected is not EffectRecipe)
            {
                return FindRig(path);
            }

            if (_current != null && _current.Recipes.Contains(path))
            {
                return _current;
            }

            return _rigs.FirstOrDefault(rig => rig.Recipes.Contains(path));
        }

        private void ShowRecipes(List<string> recipes)
        {
            _recipes.Clear();
            _noRecipes.SetHidden(recipes.Count > 0);
            var template = Layout("Recipe");
            foreach (var path in recipes)
            {
                var row = template.CloneRoot();
                row.Require<Label>("name").text = Path.GetFileName(path);
                ShowState(row.Require<Label>("state"), EffectGraphBuilder.State(path));
                row.Require<Button>("select").clicked += () => EditorAssets.Reveal(AssetDatabase.LoadAssetAtPath<EffectRecipe>(path));
                row.Require<Button>("build").clicked += () => BuildGraphs(new List<string> { path });
                _recipes.Add(row);
            }
        }

        private static void ShowState(Label label, GraphState state)
        {
            label.text = state switch
            {
                GraphState.Built => "Built",
                GraphState.OutOfDate => "Out of date",
                _ => "Missing",
            };
            label.EnableInClassList("hrts-effects__state--stale", state == GraphState.OutOfDate);
            label.EnableInClassList("hrts-effects__state--missing", state == GraphState.Missing);
        }

        private void BuildGraphs(List<string> recipes)
        {
            try
            {
                recipes.ForEach(EffectGraphBuilder.Build);
            }
            catch (Exception exception)
            {
                Say(exception.Message);
                return;
            }

            Show(_current);
        }

        // A saved recipe rebuilds after the import, since a build imports the graph it writes.
        private void OnImported(string[] imported)
        {
            var changed = _current?.Recipes.Where(imported.Contains).ToList();
            if (changed is not { Count: > 0 })
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (_playback != null)
                {
                    BuildGraphs(changed);
                }
            };
        }

        private async Task CaptureFrames()
        {
            _capture.SetEnabled(false);
            try
            {
                var capture = new EffectCapture(_current.Name);
                await capture.Render();
                EditorUtility.RevealInFinder(Path.GetFullPath(capture.Folder));
            }
            catch (Exception exception)
            {
                Say(exception.Message);
            }
            finally
            {
                _capture.SetEnabled(true);
            }
        }

        private void Say(string message)
        {
            _hint.text = message;
            _hint.SetHidden(message.Length == 0);
        }
    }
}
