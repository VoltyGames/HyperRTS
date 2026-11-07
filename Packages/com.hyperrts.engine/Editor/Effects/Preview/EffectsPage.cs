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
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private const float LongestFrame = 0.1f;
        private const long ReloadDelayMilliseconds = 500;
        private const string PickHint = "Pick an effect from the list.";

        private readonly Dictionary<int, EffectRigEntry> _rows = new();
        private List<EffectRigEntry> _rigs = new();
        private EffectRigEntry _current;
        private string _filter = "";
        private EffectPreviewPlayer _player;
        private double _lastTime;
        private float _sinceBurst;
        private bool _reloadPending;
        private VisualElement _root;
        private PageShell _shell;
        private TreeView _tree;
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

            _root.Require<ToolbarSearchField>("search").RegisterValueChangedCallback(change =>
            {
                _filter = change.newValue;
                ShowList();
            });

            _tree = _root.Require<TreeView>("list");
            _tree.makeItem = Layout("Row").CloneRoot;
            _tree.bindItem = BindRow;
            _tree.selectionChanged += _ => Pick();

            EditorState.Bind(_root);
            _root.Require<Toggle>("pause").RegisterValueChangedCallback(change => _player.Paused = change.newValue);
            _root.Require<PropertyField>("scale").RegisterValueChangeCallback(_ => ApplySettings());
            _root.Require<PropertyField>("wind-strength").RegisterValueChangeCallback(_ => ApplySettings());
            _root.Require<PropertyField>("wind-heading").RegisterValueChangeCallback(_ => ApplySettings());
            _root.Require<Button>("replay").clicked += () => Replay(true);
            _capture.clicked += () => _ = CaptureFrames();

            _root.WhileAttached(Subscribe, Unsubscribe);
            Reload();
            return _root;
        }

        private void Subscribe()
        {
            _player = new EffectPreviewPlayer(SceneManager.GetActiveScene());
            _lastTime = EditorApplication.timeSinceStartup;
            ApplySettings();
            EditorApplication.update += Tick;
            EditorApplication.projectChanged += OnProjectChange;
            Selection.selectionChanged += OnSelectionChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
            EffectPreviewImport.Imported += OnImported;
            Show(_rigs.FirstOrDefault(rig => rig.Path == Settings.lastEffect));
        }

        // Disposing the player puts the wind and fog globals back.
        private void Unsubscribe()
        {
            EditorApplication.update -= Tick;
            EditorApplication.projectChanged -= OnProjectChange;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            EffectPreviewImport.Imported -= OnImported;
            _player.Dispose();
            _player = null;
            _reloadPending = false;
        }

        private void OnProjectChange()
        {
            if (_reloadPending)
            {
                return;
            }

            _reloadPending = true;
            _root.schedule.Execute(Reload).ExecuteLater(ReloadDelayMilliseconds);
        }

        private void Reload()
        {
            _reloadPending = false;
            _rigs = EffectRigEntry.Find();
            _current = _rigs.FirstOrDefault(rig => rig.Path == _current?.Path);
            _shell.ShowEmpty(_rigs.Count == 0);
            ShowList();
        }

        private void ShowList()
        {
            _rows.Clear();
            var id = 0;
            var roots = new List<TreeViewItemData<string>>();
            var shown = _rigs.Where(rig => rig.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase));
            foreach (var kind in shown.GroupBy(rig => rig.Kind))
            {
                var kindId = id++;
                var children = new List<TreeViewItemData<string>>();
                foreach (var rig in kind)
                {
                    _rows[id] = rig;
                    children.Add(new TreeViewItemData<string>(id++, rig.Name));
                }

                roots.Add(new TreeViewItemData<string>(kindId, kind.Key, children));
            }

            _tree.SetRootItems(roots);
            _tree.Rebuild();
            _tree.ExpandAll();
            SelectRow(_current);
        }

        private void BindRow(VisualElement row, int index)
        {
            row.Require<Label>("name").text = _tree.GetItemDataForIndex<string>(index);
            var glyph = row.Require<VisualElement>("glyph");
            glyph.ClearClassList();
            glyph.AddToClassList("hrts-glyph");
            var effect = _rows.ContainsKey(_tree.GetIdForIndex(index));
            glyph.AddToClassList(effect ? "hrts-glyph--sparkles" : "hrts-glyph--folder-open");
        }

        private void SelectRow(EffectRigEntry rig)
        {
            var row = _rows.FirstOrDefault(pair => pair.Value.Path == rig?.Path);
            if (row.Value == null)
            {
                _tree.ClearSelection();
                return;
            }

            _tree.SetSelectionByIdWithoutNotify(new[] { row.Key });
            _tree.ScrollToItemById(row.Key);
        }

        private void Pick()
        {
            if (_tree.selectedIndex >= 0 && _rows.TryGetValue(_tree.GetIdForIndex(_tree.selectedIndex), out var rig))
            {
                Show(rig);
            }
        }

        private void Show(EffectRigEntry rig)
        {
            _current = rig;
            var prefab = rig == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(rig.Path);
            var loaded = _player.Load(prefab);
            _controls.SetHidden(!loaded);
            Say(loaded ? string.Empty : PickHint);
            if (!loaded)
            {
                return;
            }

            Settings.lastEffect = rig.Path;
            EditorState.instance.Save();
            _title.text = $"{rig.Name} ({rig.Kind})";
            ShowRecipes(prefab);
            _issues.Show(EffectRig.Problems(prefab).Select(problem => new ValidationIssue(MessageType.Error, problem, prefab)).ToList());
            _issues.SetHidden(_issues.childCount == 0);
            Replay(true);
        }

        private void ShowRecipes(GameObject prefab)
        {
            _recipes.Clear();
            var recipes = EffectGraphBuilder.RecipesFor(prefab);
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

        // Rebuilds after the import, since a build imports the graph it writes.
        private void OnImported(string[] imported)
        {
            if (_current == null)
            {
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_current.Path);
            var changed = EffectGraphBuilder.RecipesFor(prefab).Where(recipe => Array.IndexOf(imported, recipe) >= 0).ToList();
            if (changed.Count > 0)
            {
                EditorApplication.delayCall += () => RebuildIfShown(changed);
            }
        }

        private void RebuildIfShown(List<string> recipes)
        {
            if (_player != null)
            {
                BuildGraphs(recipes);
            }
        }

        private async Task CaptureFrames()
        {
            _capture.SetEnabled(false);
            try
            {
                await EffectCapture.Capture(_current.Name, EffectCapture.DefaultTimes, EffectCapture.DefaultDistance);
                EditorUtility.RevealInFinder(Path.GetFullPath(Path.Combine(EffectCapture.Folder, _current.Name)));
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

        private void Replay(bool restart)
        {
            var view = SceneView.lastActiveSceneView;
            _player.Pivot = view != null ? view.pivot : Vector3.zero;
            _player.Play(restart);
            _sinceBurst = 0f;
        }

        // The binding raises these on attach, before Subscribe has made the player.
        private void ApplySettings()
        {
            if (_player == null)
            {
                return;
            }

            _player.Scale = Settings.scale;
            _player.SetWind(Settings.windHeading, Settings.windStrength);
        }

        private void OnSelectionChanged()
        {
            if (!Settings.follow || Selection.activeObject == null)
            {
                return;
            }

            var rig = FollowTarget(Selection.activeObject);
            if (rig != null && rig.Path != _current?.Path)
            {
                SelectRow(rig);
                Show(rig);
            }
        }

        // A recipe stays on the current effect when that effect uses it.
        private EffectRigEntry FollowTarget(Object selected)
        {
            var path = AssetDatabase.GetAssetPath(selected);
            if (selected is not EffectRecipe)
            {
                return _rigs.FirstOrDefault(rig => rig.Path == path);
            }

            if (_current != null && _current.Recipes.Contains(path))
            {
                return _current;
            }

            return _rigs.FirstOrDefault(rig => rig.Recipes.Contains(path));
        }

        private void OnSceneChanged(Scene previous, Scene current)
        {
            _player.Dispose();
            _player = new EffectPreviewPlayer(current);
            ApplySettings();
            Show(_current);
        }

        // The recipe inspector's Preview button asks for an effect by setting the remembered one.
        private void ShowRequested()
        {
            var requested = _rigs.FirstOrDefault(rig => rig.Path == Settings.lastEffect);
            if (requested != null && requested.Path != _current?.Path)
            {
                SelectRow(requested);
                Show(requested);
            }
        }

        private void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Min((float)(now - _lastTime), LongestFrame);
            _lastTime = now;
            ShowRequested();
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
