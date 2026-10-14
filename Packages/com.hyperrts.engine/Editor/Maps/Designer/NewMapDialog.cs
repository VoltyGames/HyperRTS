using System;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Spec;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Tools ▸ Maps ▸ New map: name, mode, size, symmetry, climate and seed. Writes
    /// <c>&lt;folder&gt;/&lt;Name&gt;/&lt;Name&gt;.map.json</c> from <see cref="MapSpecTemplate"/> into a folder the user
    /// picks, then opens it in the Map Editor.
    /// </summary>
    public sealed class NewMapDialog : EditorWindow
    {
        private const int MaxSeed = 99999;
        private static readonly Vector2 WindowSize = new(360f, 230f);
        private static readonly Vector2Int DefaultSize = new(400, 400);

        private TextField _name;
        private PopupField<string> _mode;
        private Vector2IntField _size;
        private PopupField<string> _symmetry;
        private PopupField<string> _climate;
        private IntegerField _seed;
        private Label _error;
        private MapLibrary _library;

        public static void Open()
        {
            var dialog = CreateInstance<NewMapDialog>();
            dialog.titleContent = new GUIContent("New Map");
            dialog.minSize = WindowSize;
            dialog.ShowUtility();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.AddStyles(root);
            root.AddToClassList("hrts-new-map");
            _library = MapLibrary.Load();
            var climates = _library.Keys<MapClimate>().OrderBy(key => key).ToList();

            _name = new TextField("Name") { tooltip = "PascalCase, unique: names the folder, the spec and the scene." };
            _mode = new PopupField<string>("Mode", MapSpecTemplate.Modes.ToList(), 0);
            _size = new Vector2IntField("Size (m)") { value = DefaultSize, tooltip = "Playable width (X) and depth (Z)." };
            _symmetry = new PopupField<string>("Symmetry", MapSpecTemplate.Symmetries.ToList(), 0)
            {
                tooltip = "Team modes take rotate-180 or a mirror (a sector per team), FFA rotate-N (a sector per player).",
            };
            _climate = new PopupField<string>("Climate", climates, climates.Count > 0 ? 0 : -1);
            _seed = new IntegerField("Seed") { value = UnityEngine.Random.Range(1, MaxSeed) };
            _error = new Label();
            _error.AddToClassList("hrts-new-map__error");
            foreach (var field in new VisualElement[] { _name, _mode, _size, _symmetry, _climate, _seed, _error })
            {
                root.Add(field);
            }

            var create = new Button(Create) { text = "Create..." };
            create.AddToClassList("hrts-new-map__create");
            root.Add(create);
        }

        private void Create()
        {
            try
            {
                var name = _name.value.Trim();
                if (MapPaths.All().Any(path => MapPaths.MapName(path) == name))
                {
                    throw new ArgumentException($"A map named '{name}' already exists.");
                }

                var spec = MapSpecTemplate.Create(name, _mode.value, _size.value, _symmetry.value, _climate.value,
                    FirstWeather(_climate.value), _seed.value);
                var folder = PickFolder();
                if (folder == null)
                {
                    return;
                }

                var specPath = Write(folder, spec);
                Close();
                MapEditorWindow.Open(specPath);
            }
            catch (ArgumentException exception)
            {
                _error.text = exception.Message;
            }
        }

        /// <summary>A new map starts in its climate's first weather; the spec's <c>weather</c> key changes it.</summary>
        private string FirstWeather(string climate)
        {
            _library.Get<MapClimate>(climate, null, out var refs);
            var weathers = refs.weathers;
            return weathers.Count > 0 ? weathers[0].key : null;
        }

        /// <summary>The project folder the map's own folder goes in, or null when the user cancels.</summary>
        private static string PickFolder()
        {
            var picked = EditorUtility.OpenFolderPanel("Folder for the new map", DefaultFolder(), string.Empty);
            if (string.IsNullOrEmpty(picked))
            {
                return null;
            }

            var assets = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            var full = Path.GetFullPath(picked).Replace('\\', '/');
            var underAssets = full == assets || full.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase);
            if (!underAssets)
            {
                throw new ArgumentException($"Pick a folder under Assets, not '{picked}'.");
            }

            return "Assets" + full.Substring(assets.Length);
        }

        /// <summary>Where existing specs live: the parent of the first map's folder, else Assets.</summary>
        private static string DefaultFolder()
        {
            var spec = MapPaths.All().FirstOrDefault();
            return spec == null ? "Assets" : Path.GetDirectoryName(Path.GetDirectoryName(spec));
        }

        private static string Write(string folder, MapSpec spec)
        {
            var directory = $"{folder}/{spec.name}";
            var specPath = $"{directory}/{spec.name}{MapPaths.SpecExtension}";
            if (File.Exists(specPath))
            {
                throw new ArgumentException($"'{specPath}' already exists.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(specPath, MapSpecJson.Write(spec));
            AssetDatabase.Refresh();
            return specPath;
        }
    }
}
