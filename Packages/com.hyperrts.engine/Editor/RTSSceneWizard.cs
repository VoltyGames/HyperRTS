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
            EditorAssets.Layout(typeof(RTSSceneWizard)).CloneTree(root);
            _mapSize = root.Require<Vector2Field>("map-size");
            _players = root.Require<SliderInt>("players");
            _players.highValue = FactionRelations.MaxTeams - 1;
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
