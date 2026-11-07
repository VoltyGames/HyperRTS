using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.GameEntities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Scenes
{
    /// <summary>The New Blank Scene dialog over <see cref="SceneBuilder"/>, opened from Tools ▸ Maps.</summary>
    public class SceneWizard : EditorWindow
    {
        private const string DialogTitle = "New Blank Scene";

        private Vector2Field _mapSize;
        private SliderInt _players;
        private ObjectField _startingBase;

        public static void Open()
        {
            var window = GetWindow<SceneWizard>(true, DialogTitle);
            window.minSize = new Vector2(360f, 140f);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.Layout(typeof(SceneWizard)).CloneTree(root);
            var defaults = new SceneSpec();
            _mapSize = root.Require<Vector2Field>("map-size");
            _mapSize.value = defaults.MapSize;
            _players = root.Require<SliderInt>("players");
            _players.highValue = OwnerRelations.MaxTeams - 1;
            _players.value = defaults.Players;
            _startingBase = root.Require<ObjectField>("starting-base");
            _startingBase.objectType = typeof(GameEntityAuthoring);
            root.Require<Button>("create").clicked += Create;
        }

        private void Create()
        {
            var path = EditorUtility.SaveFilePanelInProject(DialogTitle, "NewRTSScene", "unity",
                "Choose where to save the scene. Its SubScene is saved next to it.");
            if (string.IsNullOrEmpty(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var subScene = SceneBuilder.Build(new SceneSpec
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
