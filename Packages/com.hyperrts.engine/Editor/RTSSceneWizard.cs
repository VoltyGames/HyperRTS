using HyperRTS.Core;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor
{
    /// <summary>The New Blank Scene dialog over <see cref="RTSSceneBuilder"/>, opened from Tools ▸ Maps.</summary>
    public class RTSSceneWizard : EditorWindow
    {
        private const string DialogTitle = "New Blank Scene";

        private Vector2Field _mapSize;
        private SliderInt _players;
        private ObjectField _startingBase;

        public static void Open()
        {
            var window = GetWindow<RTSSceneWizard>(true, DialogTitle);
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
            var path = EditorUtility.SaveFilePanelInProject(DialogTitle, "NewRTSScene", "unity",
                "Choose where to save the scene. Its SubScene is saved next to it.");
            if (string.IsNullOrEmpty(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var subScene = RTSSceneBuilder.Build(new RTSSceneSpec
            {
                ScenePath = path,
                MapSize = _mapSize.value,
                Players = _players.value,
                StartingBase = (GameEntityAuthoring)_startingBase.value,
            });

            UnityEditor.Selection.activeGameObject = subScene.gameObject;
            Debug.Log("HyperRTS: scene created. Open the SubScene (tick its checkbox) and add units and buildings " +
                      "with GameObject ▸ HyperRTS. See " + HyperRTSDocs.GettingStarted);
            Close();
        }
    }
}
