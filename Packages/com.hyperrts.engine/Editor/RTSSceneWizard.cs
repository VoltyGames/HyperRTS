using HyperRTS.Core;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor
{
    /// <summary>HyperRTS ▸ Create RTS Scene: the window over <see cref="RTSSceneBuilder"/>.</summary>
    public class RTSSceneWizard : EditorWindow
    {
        private Vector2Field _mapSize;
        private SliderInt _players;
        private ObjectField _startingBase;

        [MenuItem(EditorMenu.CreateScene, false, EditorMenu.CreateScenePriority)]
        private static void Open()
        {
            var window = GetWindow<RTSSceneWizard>(true, "Create RTS Scene");
            window.minSize = new Vector2(360f, 140f);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.AddStyles(root);
            root.AddToClassList("hrts-note");

            _mapSize = new Vector2Field("Map Size") { value = new Vector2(200f, 200f) };
            _mapSize.tooltip = "Playable area (X by Z); the ground is sized to match.";
            _players = new SliderInt("Players", 1, FactionRelations.MaxTeams - 1) { value = 2, showInputField = true };
            _players.tooltip = "Player 1 is you, the rest are AI on their own teams.";
            _startingBase = new ObjectField("Starting Base")
            {
                objectType = typeof(GameEntityAuthoring),
                allowSceneObjects = false,
                tooltip = "Optional building prefab placed for every player around the map (a command centre).",
            };

            root.Add(_mapSize);
            root.Add(_players);
            root.Add(_startingBase);
            var buttons = new VisualElement();
            buttons.AddToClassList("hrts-buttons");
            buttons.Add(new Button(Create) { text = "Create" });
            root.Add(buttons);
        }

        private void Create()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create RTS Scene", "NewRTSScene", "unity",
                "Choose where to save the scene. Its SubScene is saved next to it.");
            if (string.IsNullOrEmpty(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            RTSSceneBuilder.Build(new RTSSceneSpec
            {
                ScenePath = path,
                MapSize = _mapSize.value,
                Players = _players.value,
                StartingBase = (GameEntityAuthoring)_startingBase.value,
            });

            UnityEditor.Selection.activeGameObject = FindAnyObjectByType<SubScene>().gameObject;
            Debug.Log("HyperRTS: scene created. Open the SubScene (tick its checkbox) and add units and buildings " +
                      "with GameObject ▸ HyperRTS. See " + HyperRTSDocs.GettingStarted);
            Close();
        }
    }
}
